using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Surveys.Contracts;
using NeighborGoods.Api.Shared.ApiContracts;
using NeighborGoods.Api.Shared.Security;
using NeighborGoods.Data;
using NeighborGoods.Data.Surveys;

namespace NeighborGoods.Api.Features.Surveys;

public static partial class SurveyEndpoints
{
    public const string DefaultActivityName = "鄰里雲端便利換計劃——讓社區流動起來！";
    public const string GiftName = "一瓶御茶園麥萃日式麥茶";
    public const string PickupNote = "贈品將放置於用餐區，請自行取用。每人限領一瓶。";
    public const int GiftLimit = 24;

    /// <summary>
    /// 可選：站內贈品刊登路徑（例如 /listings/{guid}）。
    /// 刊登完成後把網址路徑填在這裡，問卷頁會多顯示「也可到商品頁領取」。
    /// </summary>
    public const string GiftListingPath = "";

    private static readonly HashSet<string> ResidentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "social",
        "non_social",
    };

    private static readonly HashSet<string> Buildings = new(StringComparer.OrdinalIgnoreCase)
    {
        "south",
        "north",
    };

    private static readonly HashSet<string> Ratings = new(StringComparer.OrdinalIgnoreCase)
    {
        "satisfied",
        "fair",
        "unsatisfied",
    };

    private static readonly HashSet<string> Channels = new(StringComparer.OrdinalIgnoreCase)
    {
        "friends",
        "bulletin",
        "youth_online",
        "other",
    };

    public static IEndpointRouteBuilder MapSurveyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/surveys/gift-status", GetGiftStatusAsync)
            .WithName("GetSurveyGiftStatusV1")
            .RequireAuthorization();

        app.MapPost("/api/v1/surveys", SubmitSurveyAsync)
            .WithName("SubmitSurveyV1")
            .RequireAuthorization()
            .RequireRateLimiting("SurveyWrite");

        return app;
    }

    private static async Task<IResult> GetGiftStatusAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        CancellationToken ct = default)
    {
        var userId = currentUser.GetRequiredUserId();
        var claimed = await dbContext.SurveyResponses.AsNoTracking().CountAsync(x => x.IsGiftEligible, ct);
        var remaining = Math.Max(0, GiftLimit - claimed);
        var listingPath = string.IsNullOrWhiteSpace(GiftListingPath) ? null : GiftListingPath.Trim();
        var existing = await dbContext.SurveyResponses.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.IsGiftEligible })
            .FirstOrDefaultAsync(ct);
        var payload = new SurveyGiftStatusResponse(
            GiftName,
            PickupNote,
            GiftLimit,
            remaining,
            remaining > 0,
            listingPath,
            existing is not null,
            existing?.IsGiftEligible == true);
        httpContext.Response.Headers.CacheControl = "no-store";
        return Results.Ok(ApiResponseFactory.Success(payload, httpContext));
    }

    private static async Task<IResult> SubmitSurveyAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        SubmitSurveyRequest request,
        NeighborGoodsDbContext dbContext,
        CancellationToken ct = default)
    {
        var userId = currentUser.GetRequiredUserId();
        var error = Validate(request);
        if (error is not null)
        {
            return Results.Json(
                ApiResponseFactory.Error("VALIDATION_ERROR", error, httpContext),
                statusCode: StatusCodes.Status400BadRequest);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        var alreadySubmitted = await dbContext.SurveyResponses.AnyAsync(x => x.UserId == userId, ct);
        if (alreadySubmitted)
        {
            return Results.Json(
                ApiResponseFactory.Error("SURVEY_ALREADY_SUBMITTED", "此帳號已填寫過問卷，每人限填一次、限領一瓶。", httpContext),
                statusCode: StatusCodes.Status409Conflict);
        }

        var claimed = await dbContext.SurveyResponses.CountAsync(x => x.IsGiftEligible, ct);
        var isGiftEligible = claimed < GiftLimit;

        var isSocial = string.Equals(request.ResidentType, "social", StringComparison.OrdinalIgnoreCase);
        var entity = new SurveyResponse
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            ResidentType = request.ResidentType.Trim().ToLowerInvariant(),
            DisplayName = NormalizeOptional(request.DisplayName, 100),
            Phone = NormalizeOptional(request.Phone, 32),
            Building = isSocial ? request.Building!.Trim().ToLowerInvariant() : null,
            Floor = isSocial ? request.Floor : null,
            RatingUi = request.RatingUi.Trim().ToLowerInvariant(),
            RatingListing = request.RatingListing.Trim().ToLowerInvariant(),
            RatingInteraction = request.RatingInteraction.Trim().ToLowerInvariant(),
            RatingGuide = request.RatingGuide.Trim().ToLowerInvariant(),
            RatingPromo = request.RatingPromo.Trim().ToLowerInvariant(),
            Channel = request.Channel.Trim().ToLowerInvariant(),
            Feedback = NormalizeOptional(request.Feedback, 2000),
            ConsentAccepted = true,
            ActivityName = DefaultActivityName,
            IsGiftEligible = isGiftEligible,
        };

        dbContext.SurveyResponses.Add(entity);
        try
        {
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            return Results.Json(
                ApiResponseFactory.Error("SURVEY_ALREADY_SUBMITTED", "此帳號已填寫過問卷，每人限填一次、限領一瓶。", httpContext),
                statusCode: StatusCodes.Status409Conflict);
        }

        var remaining = Math.Max(0, GiftLimit - (claimed + (isGiftEligible ? 1 : 0)));
        return Results.Ok(ApiResponseFactory.Success(
            new SurveySubmitResponse(entity.Id, entity.CreatedAt, entity.IsGiftEligible, remaining),
            httpContext));
    }

    private static string? Validate(SubmitSurveyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ResidentType) || !ResidentTypes.Contains(request.ResidentType.Trim()))
        {
            return "請選擇身份";
        }

        if (!request.ConsentAccepted)
        {
            return "請勾選同意蒐集個資";
        }

        var isSocial = string.Equals(request.ResidentType, "social", StringComparison.OrdinalIgnoreCase);
        if (isSocial)
        {
            if (string.IsNullOrWhiteSpace(request.Building) || !Buildings.Contains(request.Building.Trim()))
            {
                return "請選擇棟別";
            }

            if (request.Floor is null or < 1 or > 50)
            {
                return "請填寫正確樓層";
            }
        }

        if (!IsValidRating(request.RatingUi)
            || !IsValidRating(request.RatingListing)
            || !IsValidRating(request.RatingInteraction)
            || !IsValidRating(request.RatingGuide)
            || !IsValidRating(request.RatingPromo))
        {
            return "請完成所有滿意度評分";
        }

        if (string.IsNullOrWhiteSpace(request.Channel) || !Channels.Contains(request.Channel.Trim()))
        {
            return "請選擇得知管道";
        }

        var phone = request.Phone?.Trim();
        if (!string.IsNullOrEmpty(phone) && !PhoneRegex().IsMatch(phone))
        {
            return "手機格式不正確";
        }

        return null;
    }

    private static bool IsValidRating(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Ratings.Contains(value.Trim());

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    [GeneratedRegex(@"^09\d{8}$")]
    private static partial Regex PhoneRegex();
}
