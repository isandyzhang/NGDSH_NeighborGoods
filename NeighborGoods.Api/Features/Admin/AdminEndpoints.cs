using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Admin.Contracts.Requests;
using NeighborGoods.Api.Features.Admin.Contracts.Responses;
using NeighborGoods.Api.Features.Admin.Services;
using NeighborGoods.Api.Features.Integrations.Ado;
using NeighborGoods.Api.Features.Integrations.Ado.Contracts;
using NeighborGoods.Api.Features.Announcements.Contracts;
using NeighborGoods.Api.Features.Announcements.Services;
using NeighborGoods.Api.Features.Listing;
using NeighborGoods.Api.Features.Listing.Contracts;
using NeighborGoods.Api.Features.Messaging;
using NeighborGoods.Api.Features.Messaging.Contracts.Responses;
using NeighborGoods.Api.Infrastructure.Storage;
using NeighborGoods.Api.Shared.ApiContracts;
using NeighborGoods.Data;
using NeighborGoods.Data.Announcements;
using NeighborGoods.Api.Shared.Security;

namespace NeighborGoods.Api.Features.Admin;

public static class AdminEndpoints
{
    private const int AdminRoleCode = 3;
    private const int TopSubmissionPendingCode = 0;
    private const string AdminMessageDisplayName = "管理員";

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/dashboard", GetAdminDashboardAsync)
        .WithName("GetAdminDashboardV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/admin/listings/{id:guid}/force-status", ForceListingStatusAsync)
        .WithName("AdminForceListingStatusV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/admin/listings/batch-status", BatchForceListingStatusAsync)
        .WithName("AdminBatchForceListingStatusV1")
        .RequireAuthorization();

        app.MapDelete("/api/v1/admin/listings/{id:guid}/hard-delete", HardDeleteListingAsync)
        .WithName("AdminHardDeleteListingV1")
        .RequireAuthorization();

        app.MapGet("/api/v1/admin/listings", GetAdminListingsAsync)
        .WithName("AdminGetListingsV1")
        .RequireAuthorization();

        app.MapGet("/api/v1/admin/listings/{id:guid}", GetAdminListingDetailAsync)
        .WithName("AdminGetListingDetailV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/admin/listings/{id:guid}", UpdateAdminListingAsync)
        .WithName("AdminUpdateListingV1")
        .RequireAuthorization();

        app.MapGet("/api/v1/admin/members", GetAdminMembersAsync)
        .WithName("AdminGetMembersV1")
        .RequireAuthorization();

        app.MapGet("/api/v1/admin/announcements", GetAdminAnnouncementsAsync)
        .WithName("AdminGetAnnouncementsV1")
        .RequireAuthorization();

        app.MapPost("/api/v1/admin/announcements", CreateAdminAnnouncementAsync)
        .WithName("AdminCreateAnnouncementV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/admin/announcements/{id:guid}", UpdateAdminAnnouncementAsync)
        .WithName("AdminUpdateAnnouncementV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/admin/announcements/{id:guid}/enabled", SetAdminAnnouncementEnabledAsync)
        .WithName("AdminSetAnnouncementEnabledV1")
        .RequireAuthorization();

        app.MapDelete("/api/v1/admin/announcements/{id:guid}", DeleteAdminAnnouncementAsync)
        .WithName("AdminDeleteAnnouncementV1")
        .RequireAuthorization();

        app.MapGet("/api/v1/admin/conversations", GetAdminConversationsAsync)
        .WithName("AdminGetConversationsV1")
        .RequireAuthorization();

        app.MapGet("/api/v1/admin/conversations/by-listing", GetAdminConversationsByListingAsync)
        .WithName("AdminGetConversationsByListingV1")
        .RequireAuthorization();

        app.MapGet("/api/v1/admin/conversations/{conversationId:guid}/messages", GetAdminConversationMessagesAsync)
        .WithName("AdminGetConversationMessagesV1")
        .RequireAuthorization();

        app.MapPost("/api/v1/admin/conversations/{conversationId:guid}/messages", PostAdminConversationMessageAsync)
        .WithName("AdminPostConversationMessageV1")
        .RequireAuthorization();

        app.MapGet("/api/v1/admin/ado-webhook-events", GetAdminAdoWebhookEventsAsync)
        .WithName("AdminGetAdoWebhookEventsV1")
        .RequireAuthorization();

        app.MapGet("/api/v1/admin/ado-webhook-events/{id:guid}", GetAdminAdoWebhookEventDetailAsync)
        .WithName("AdminGetAdoWebhookEventDetailV1")
        .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> GetAdminDashboardAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUser, dbContext, ct);
        if (!isAdmin)
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var totalListings = await dbContext.Listings.AsNoTracking().CountAsync(ct);
        var now = DateTime.UtcNow;
        var sevenDaysAgo = now.AddDays(-7);
        var activeListings = await dbContext.Listings.AsNoTracking().CountAsync(x => x.Status == (int)ListingStatus.Active, ct);
        var soldListings = await dbContext.Listings.AsNoTracking().CountAsync(x => x.Status == (int)ListingStatus.Sold, ct);
        var donatedListings = await dbContext.Listings.AsNoTracking().CountAsync(x => x.Status == (int)ListingStatus.Donated, ct);
        var givenOrTradedListings = await dbContext.Listings.AsNoTracking().CountAsync(x => x.Status == (int)ListingStatus.GivenOrTraded, ct);
        var activeListingsLast7Days = await dbContext.Listings.AsNoTracking()
            .CountAsync(x => x.Status == (int)ListingStatus.Active && x.CreatedAt >= sevenDaysAgo, ct);
        var soldListingsLast7Days = await dbContext.Listings.AsNoTracking()
            .CountAsync(x => x.Status == (int)ListingStatus.Sold && x.UpdatedAt >= sevenDaysAgo, ct);
        var donatedListingsLast7Days = await dbContext.Listings.AsNoTracking()
            .CountAsync(x => x.Status == (int)ListingStatus.Donated && x.UpdatedAt >= sevenDaysAgo, ct);
        var givenOrTradedListingsLast7Days = await dbContext.Listings.AsNoTracking()
            .CountAsync(x => x.Status == (int)ListingStatus.GivenOrTraded && x.UpdatedAt >= sevenDaysAgo, ct);

        var totalMembers = await dbContext.AspNetUsers.AsNoTracking().CountAsync(ct);
        var passwordLoginMembers = await dbContext.AspNetUsers.AsNoTracking().CountAsync(x => x.PasswordHash != null && x.PasswordHash != "", ct);
        var lineLoginMembers = await dbContext.AspNetUsers.AsNoTracking().CountAsync(x => x.LineUserId != null && x.LineUserId != "", ct);
        var emailBoundMembers = await dbContext.AspNetUsers.AsNoTracking().CountAsync(x => x.EmailConfirmed, ct);
        var lineOfficialBoundMembers = await dbContext.AspNetUsers.AsNoTracking().CountAsync(x => x.LineMessagingApiAuthorizedAt != null, ct);

        var oneDayAgo = now.AddDays(-1);
        var thirtyDaysAgo = now.AddDays(-30);
        var activeMembers24h = await dbContext.AspNetUsers.AsNoTracking().CountAsync(x => x.LastLoginAt != null && x.LastLoginAt >= oneDayAgo, ct);
        var activeMembers7d = await dbContext.AspNetUsers.AsNoTracking().CountAsync(x => x.LastLoginAt != null && x.LastLoginAt >= sevenDaysAgo, ct);
        var activeMembers30d = await dbContext.AspNetUsers.AsNoTracking().CountAsync(x => x.LastLoginAt != null && x.LastLoginAt >= thirtyDaysAgo, ct);
        var emailedMembers24h = await dbContext.AspNetUsers.AsNoTracking().CountAsync(x => x.EmailNotificationLastSentAt != null && x.EmailNotificationLastSentAt >= oneDayAgo, ct);
        var emailedMembers7d = await dbContext.AspNetUsers.AsNoTracking().CountAsync(x => x.EmailNotificationLastSentAt != null && x.EmailNotificationLastSentAt >= sevenDaysAgo, ct);
        var emailedMembers30d = await dbContext.AspNetUsers.AsNoTracking().CountAsync(x => x.EmailNotificationLastSentAt != null && x.EmailNotificationLastSentAt >= thirtyDaysAgo, ct);
        var lineNotifiedMembers24h = await dbContext.AspNetUsers.AsNoTracking().CountAsync(x => x.LineNotificationLastSentAt != null && x.LineNotificationLastSentAt >= oneDayAgo, ct);
        var lineNotifiedMembers7d = await dbContext.AspNetUsers.AsNoTracking().CountAsync(x => x.LineNotificationLastSentAt != null && x.LineNotificationLastSentAt >= sevenDaysAgo, ct);
        var lineNotifiedMembers30d = await dbContext.AspNetUsers.AsNoTracking().CountAsync(x => x.LineNotificationLastSentAt != null && x.LineNotificationLastSentAt >= thirtyDaysAgo, ct);
        var pendingTopSubmissions = await dbContext.ListingTopSubmissions.AsNoTracking().CountAsync(x => x.Status == TopSubmissionPendingCode, ct);
        var unreadAdminMessages = await dbContext.AdminMessages.AsNoTracking().CountAsync(x => !x.IsRead, ct);

        var latestListings = await dbContext.Listings
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new AdminDashboardListingResponse(x.Id, x.Title, x.Seller.DisplayName, x.Price, x.IsFree, x.Status, x.IsPinned, x.CreatedAt))
            .ToListAsync(ct);
        var latestMessages = await dbContext.AdminMessages
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new AdminDashboardMessageResponse(x.Id, x.Sender.DisplayName, x.Content, x.IsRead, x.CreatedAt))
            .ToListAsync(ct);
        var latestTopSubmissions = await dbContext.ListingTopSubmissions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new AdminDashboardTopSubmissionResponse(x.Id, x.User.DisplayName, x.ListingId, x.FeedbackTitle, x.Status, x.CreatedAt))
            .ToListAsync(ct);

        var payload = new AdminDashboardResponse(
            new AdminDashboardKpiResponse(
                totalListings,
                activeListings,
                soldListings,
                donatedListings,
                givenOrTradedListings,
                activeListingsLast7Days,
                soldListingsLast7Days,
                donatedListingsLast7Days,
                givenOrTradedListingsLast7Days,
                totalMembers,
                passwordLoginMembers,
                lineLoginMembers,
                emailBoundMembers,
                lineOfficialBoundMembers,
                activeMembers24h,
                activeMembers7d,
                activeMembers30d,
                emailedMembers24h,
                emailedMembers7d,
                emailedMembers30d,
                lineNotifiedMembers24h,
                lineNotifiedMembers7d,
                lineNotifiedMembers30d,
                pendingTopSubmissions,
                unreadAdminMessages),
            latestListings,
            latestMessages,
            latestTopSubmissions);

        return Results.Ok(ApiResponseFactory.Success(payload, httpContext));
    }

    private static async Task<IResult> ForceListingStatusAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        Guid id,
        AdminForceListingStatusRequest request,
        CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUser, dbContext, ct);
        if (!isAdmin)
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!Enum.IsDefined(typeof(ListingStatus), request.Status))
        {
            return Results.BadRequest(ApiResponseFactory.Error("VALIDATION_ERROR", "無效的商品狀態代碼", httpContext));
        }

        var listing = await dbContext.Listings.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (listing is null)
        {
            return Results.NotFound(ApiResponseFactory.Error("LISTING_NOT_FOUND", "找不到商品", httpContext));
        }

        listing.Status = request.Status;
        listing.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return Results.Ok(ApiResponseFactory.Success(new { id, status = listing.Status, forced = true }, httpContext));
    }

    private static async Task<IResult> HardDeleteListingAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        IBlobStorage blobStorage,
        Guid id,
        CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUser, dbContext, ct);
        if (!isAdmin)
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var listingExists = await dbContext.Listings.AsNoTracking().AnyAsync(x => x.Id == id, ct);
        if (!listingExists)
        {
            return Results.NotFound(ApiResponseFactory.Error("LISTING_NOT_FOUND", "找不到商品", httpContext));
        }

        var imageBlobNames = await dbContext.ListingImages.Where(x => x.ListingId == id).Select(x => x.ImageUrl).ToListAsync(ct);
        var conversationIds = await dbContext.Conversations.Where(x => x.ListingId == id).Select(x => x.Id).ToListAsync(ct);

        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);
        await dbContext.Reviews.Where(x => x.ListingId == id).ExecuteDeleteAsync(ct);
        await dbContext.PurchaseRequests.Where(x => x.ListingId == id).ExecuteDeleteAsync(ct);
        if (conversationIds.Count > 0)
        {
            await dbContext.Messages.Where(x => conversationIds.Contains(x.ConversationId)).ExecuteDeleteAsync(ct);
            await dbContext.Conversations.Where(x => conversationIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
        }

        await dbContext.ListingFavorites.Where(x => x.ListingId == id).ExecuteDeleteAsync(ct);
        await dbContext.ListingTopSubmissions.Where(x => x.ListingId == id).ExecuteDeleteAsync(ct);
        await dbContext.ListingImages.Where(x => x.ListingId == id).ExecuteDeleteAsync(ct);
        await dbContext.Listings.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);

        foreach (var rawUrl in imageBlobNames)
        {
            var blobName = ListingBlobPath.ToBlobNameForDeletion(rawUrl);
            if (string.IsNullOrWhiteSpace(blobName))
            {
                continue;
            }

            try
            {
                await blobStorage.DeleteAsync(blobName, ct);
            }
            catch
            {
                // hard delete 以 DB 移除為主，blob 清理失敗不阻擋主流程
            }
        }

        return Results.Ok(ApiResponseFactory.Success(new { id, deleted = true, hardDeleted = true }, httpContext));
    }

    private static async Task<IResult> BatchForceListingStatusAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminBatchListingStatusRequest request,
        CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUser, dbContext, ct);
        if (!isAdmin)
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!Enum.IsDefined(typeof(ListingStatus), request.Status))
        {
            return Results.BadRequest(ApiResponseFactory.Error("VALIDATION_ERROR", "無效的商品狀態代碼", httpContext));
        }

        var ids = request.ListingIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            return Results.BadRequest(ApiResponseFactory.Error("VALIDATION_ERROR", "請提供至少一筆商品 ID", httpContext));
        }

        var count = await dbContext.Listings
            .Where(x => ids.Contains(x.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, request.Status)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);

        return Results.Ok(ApiResponseFactory.Success(new { updatedCount = count, status = request.Status }, httpContext));
    }

    private static async Task<IResult> GetAdminListingsAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        string? q = null,
        int? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUser, dbContext, ct);
        if (!isAdmin)
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.Listings.AsNoTracking().AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            query = query.Where(x =>
                EF.Functions.Like(x.Title, $"%{keyword}%") ||
                (x.Description != null && EF.Functions.Like(x.Description, $"%{keyword}%")) ||
                EF.Functions.Like(x.Seller.DisplayName, $"%{keyword}%"));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new AdminListingManagementItemResponse(
                x.Id,
                x.Title,
                x.Seller.DisplayName,
                Convert.ToInt32(x.Price),
                x.IsFree,
                x.Status,
                x.IsPinned,
                x.CreatedAt))
            .ToListAsync(ct);

        var payload = new AdminListingManagementResponse(
            items,
            new AdminListingManagementPaginationResponse(
                normalizedPage,
                normalizedPageSize,
                totalCount,
                (int)Math.Ceiling(totalCount / (double)normalizedPageSize)));

        return Results.Ok(ApiResponseFactory.Success(payload, httpContext));
    }

    private static async Task<IResult> GetAdminListingDetailAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        Guid id,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(currentUser, dbContext, ct))
        {
            return Results.Json(ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext), statusCode: StatusCodes.Status403Forbidden);
        }

        var item = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new AdminListingDetailResponse(
                x.Id,
                x.Title,
                x.Description,
                x.Category,
                x.Condition,
                Convert.ToInt32(x.Price),
                x.Residence,
                x.PickupLocation,
                x.IsFree,
                x.IsCharity,
                x.IsTradeable,
                x.Status,
                x.SellerId,
                x.Seller.DisplayName,
                x.ListingImages
                    .OrderBy(img => img.SortOrder)
                    .Select(img => new AdminListingImageResponse(img.Id, img.ImageUrl, img.SortOrder))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

        if (item is null)
        {
            return Results.NotFound(ApiResponseFactory.Error("LISTING_NOT_FOUND", "找不到商品", httpContext));
        }

        return Results.Ok(ApiResponseFactory.Success(item, httpContext));
    }

    private static async Task<IResult> UpdateAdminListingAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        Guid id,
        AdminUpdateListingRequest request,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(currentUser, dbContext, ct))
        {
            return Results.Json(ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext), statusCode: StatusCodes.Status403Forbidden);
        }

        if (!Enum.IsDefined(typeof(ListingStatus), request.Status))
        {
            return Results.BadRequest(ApiResponseFactory.Error("VALIDATION_ERROR", "無效的商品狀態代碼", httpContext));
        }

        var listing = await dbContext.Listings.Include(x => x.ListingImages).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (listing is null)
        {
            return Results.NotFound(ApiResponseFactory.Error("LISTING_NOT_FOUND", "找不到商品", httpContext));
        }

        listing.Title = request.Title.Trim();
        listing.Description = request.Description?.Trim() ?? string.Empty;
        listing.Category = request.CategoryCode;
        listing.Condition = request.ConditionCode;
        listing.Price = request.IsFree ? 0 : Math.Max(request.Price, 0);
        listing.Residence = request.ResidenceCode;
        listing.PickupLocation = request.PickupLocationCode;
        listing.IsFree = request.IsFree || request.Price == 0;
        listing.IsCharity = request.IsCharity;
        listing.IsTradeable = request.IsTradeable;
        listing.Status = request.Status;
        listing.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);
        return Results.Ok(ApiResponseFactory.Success(new { id, updated = true, status = listing.Status }, httpContext));
    }

    private static async Task<IResult> GetAdminMembersAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        string? q = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(currentUser, dbContext, ct))
        {
            return Results.Json(ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext), statusCode: StatusCodes.Status403Forbidden);
        }

        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.AspNetUsers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            query = query.Where(x =>
                EF.Functions.Like(x.DisplayName, $"%{keyword}%") ||
                (x.Email != null && EF.Functions.Like(x.Email, $"%{keyword}%")) ||
                (x.UserName != null && EF.Functions.Like(x.UserName, $"%{keyword}%")));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new AdminMemberListItemResponse(
                x.Id,
                x.DisplayName,
                x.UserName,
                x.Email,
                x.EmailConfirmed,
                x.LineUserId,
                x.LineContactId,
                x.Role,
                x.CreatedAt,
                x.LastLoginAt,
                x.LineMessagingApiAuthorizedAt,
                x.LineNotificationPreference,
                x.TopPinCredits,
                x.IsQuickResponder,
                x.PhoneNumber,
                x.LockoutEnabled,
                !string.IsNullOrWhiteSpace(x.PasswordHash)))
            .ToListAsync(ct);

        var payload = new AdminMemberListResponse(
            items,
            normalizedPage,
            normalizedPageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)normalizedPageSize));
        return Results.Ok(ApiResponseFactory.Success(payload, httpContext));
    }

    private static async Task<IResult> GetAdminAnnouncementsAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        AnnouncementQueryService queryService,
        NeighborGoodsDbContext dbContext,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(currentUser, dbContext, ct))
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var items = await queryService.GetAllForAdminAsync(ct);
        return Results.Ok(ApiResponseFactory.Success(new AdminAnnouncementsListResponse(items), httpContext));
    }

    private static async Task<IResult> CreateAdminAnnouncementAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        UpsertAnnouncementRequest request,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(currentUser, dbContext, ct))
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var validation = AnnouncementValidation.ValidateWrite(
            request.Message,
            request.Severity,
            request.Scope,
            request.StartsAt,
            request.EndsAt,
            request.LinkUrl);
        if (!validation.IsValid)
        {
            return Results.BadRequest(ApiResponseFactory.Error(validation.ErrorCode!, validation.ErrorMessage!, httpContext));
        }

        var userId = currentUser.GetRequiredUserId();
        var now = DateTime.UtcNow;
        var entity = new SiteAnnouncement
        {
            Id = Guid.NewGuid(),
            Message = request.Message.Trim(),
            Severity = request.Severity,
            Scope = request.Scope,
            SortOrder = request.SortOrder,
            IsEnabled = request.IsEnabled,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            LinkUrl = string.IsNullOrWhiteSpace(request.LinkUrl) ? null : request.LinkUrl.Trim(),
            LinkLabel = string.IsNullOrWhiteSpace(request.LinkLabel) ? null : request.LinkLabel.Trim(),
            CreatedAt = now,
            CreatedByUserId = userId,
        };

        dbContext.SiteAnnouncements.Add(entity);
        await dbContext.SaveChangesAsync(ct);

        return Results.Ok(ApiResponseFactory.Success(ToAdminResponse(entity), httpContext));
    }

    private static async Task<IResult> UpdateAdminAnnouncementAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        Guid id,
        UpsertAnnouncementRequest request,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(currentUser, dbContext, ct))
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var validation = AnnouncementValidation.ValidateWrite(
            request.Message,
            request.Severity,
            request.Scope,
            request.StartsAt,
            request.EndsAt,
            request.LinkUrl);
        if (!validation.IsValid)
        {
            return Results.BadRequest(ApiResponseFactory.Error(validation.ErrorCode!, validation.ErrorMessage!, httpContext));
        }

        var entity = await dbContext.SiteAnnouncements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            return Results.NotFound(ApiResponseFactory.Error("ANNOUNCEMENT_NOT_FOUND", "找不到公告", httpContext));
        }

        var userId = currentUser.GetRequiredUserId();
        entity.Message = request.Message.Trim();
        entity.Severity = request.Severity;
        entity.Scope = request.Scope;
        entity.SortOrder = request.SortOrder;
        entity.IsEnabled = request.IsEnabled;
        entity.StartsAt = request.StartsAt;
        entity.EndsAt = request.EndsAt;
        entity.LinkUrl = string.IsNullOrWhiteSpace(request.LinkUrl) ? null : request.LinkUrl.Trim();
        entity.LinkLabel = string.IsNullOrWhiteSpace(request.LinkLabel) ? null : request.LinkLabel.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = userId;

        await dbContext.SaveChangesAsync(ct);
        return Results.Ok(ApiResponseFactory.Success(ToAdminResponse(entity), httpContext));
    }

    private static async Task<IResult> SetAdminAnnouncementEnabledAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        Guid id,
        SetAnnouncementEnabledRequest request,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(currentUser, dbContext, ct))
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var entity = await dbContext.SiteAnnouncements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            return Results.NotFound(ApiResponseFactory.Error("ANNOUNCEMENT_NOT_FOUND", "找不到公告", httpContext));
        }

        entity.IsEnabled = request.IsEnabled;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = currentUser.GetRequiredUserId();
        await dbContext.SaveChangesAsync(ct);

        return Results.Ok(ApiResponseFactory.Success(ToAdminResponse(entity), httpContext));
    }

    private static async Task<IResult> DeleteAdminAnnouncementAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        Guid id,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(currentUser, dbContext, ct))
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var entity = await dbContext.SiteAnnouncements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            return Results.NotFound(ApiResponseFactory.Error("ANNOUNCEMENT_NOT_FOUND", "找不到公告", httpContext));
        }

        dbContext.SiteAnnouncements.Remove(entity);
        await dbContext.SaveChangesAsync(ct);

        return Results.Ok(ApiResponseFactory.Success(new { id, deleted = true }, httpContext));
    }

    private static async Task<IResult> GetAdminConversationsAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminConversationQueryService conversationQueryService,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUser, dbContext, ct);
        if (!isAdmin)
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var result = await conversationQueryService.ListConversationsAsync(page, pageSize, ct);
        return Results.Ok(ApiResponseFactory.Success(result, httpContext));
    }

    private static async Task<IResult> GetAdminConversationsByListingAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminConversationQueryService conversationQueryService,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUser, dbContext, ct);
        if (!isAdmin)
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var result = await conversationQueryService.ListByListingAsync(page, pageSize, ct);
        return Results.Ok(ApiResponseFactory.Success(result, httpContext));
    }

    private static async Task<IResult> GetAdminConversationMessagesAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminConversationQueryService conversationQueryService,
        Guid conversationId,
        int page = 1,
        int pageSize = 100,
        string? q = null,
        CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUser, dbContext, ct);
        if (!isAdmin)
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var (data, conversationExists) = await conversationQueryService.GetMessagesAsync(
            conversationId,
            page,
            pageSize,
            q,
            ct);

        if (!conversationExists)
        {
            return Results.Json(
                ApiResponseFactory.Error("CONVERSATION_NOT_FOUND", "找不到對話", httpContext),
                statusCode: StatusCodes.Status404NotFound);
        }

        return Results.Ok(ApiResponseFactory.Success(data, httpContext));
    }

    private static async Task<IResult> PostAdminConversationMessageAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        Guid conversationId,
        AdminSendConversationMessageRequest request,
        CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUser, dbContext, ct);
        if (!isAdmin)
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var content = request.Content?.Trim() ?? string.Empty;
        if (content.Length == 0)
        {
            return Results.BadRequest(ApiResponseFactory.Error("MESSAGE_VALIDATION_FAILED", "訊息內容不可為空白。", httpContext));
        }

        if (content.Length > MessagingConstants.MaxMessageContentLength)
        {
            return Results.BadRequest(
                ApiResponseFactory.Error(
                    "MESSAGE_VALIDATION_FAILED",
                    $"訊息長度不可超過 {MessagingConstants.MaxMessageContentLength} 字元。",
                    httpContext));
        }

        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);
        if (conversation is null)
        {
            return Results.Json(
                ApiResponseFactory.Error("CONVERSATION_NOT_FOUND", "找不到對話", httpContext),
                statusCode: StatusCodes.Status404NotFound);
        }

        var senderId = currentUser.GetRequiredUserId();
        var sender = await dbContext.AspNetUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == senderId, ct);
        if (sender is null)
        {
            return Results.Json(
                ApiResponseFactory.Error("USER_NOT_FOUND", "找不到目前使用者", httpContext),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var now = DateTime.UtcNow;
        var message = new Data.LegacyEntities.Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content,
            CreatedAt = now
        };

        dbContext.Messages.Add(message);
        await dbContext.Conversations
            .Where(c => c.Id == conversationId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.UpdatedAt, now), ct);
        await dbContext.SaveChangesAsync(ct);

        var dto = new MessageItemDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = senderId,
            SenderDisplayName = AdminMessageDisplayName,
            Content = message.Content,
            CreatedAt = message.CreatedAt
        };

        return Results.Ok(ApiResponseFactory.Success(dto, httpContext));
    }

    private const int AdoWebhookPreviewLength = 80;

    private static async Task<IResult> GetAdminAdoWebhookEventsAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdoWebhookMemoryStore store,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(currentUser, dbContext, ct))
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var (items, totalCount) = store.List(normalizedPage, normalizedPageSize);
        var payload = new AdoWebhookEventListResponse(
            items.Select(ToListItemDto).ToList(),
            normalizedPage,
            normalizedPageSize,
            totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize));

        return Results.Ok(ApiResponseFactory.Success(payload, httpContext));
    }

    private static async Task<IResult> GetAdminAdoWebhookEventDetailAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdoWebhookMemoryStore store,
        Guid id,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(currentUser, dbContext, ct))
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var entry = store.GetById(id);
        if (entry is null)
        {
            return Results.NotFound(ApiResponseFactory.Error("ADO_WEBHOOK_EVENT_NOT_FOUND", "找不到 webhook 紀錄", httpContext));
        }

        return Results.Ok(ApiResponseFactory.Success(ToDetailDto(entry), httpContext));
    }

    private static AdoWebhookEventListItemDto ToListItemDto(AdoWebhookEventEntry entry) =>
        new(
            entry.Id,
            entry.ReceivedAt,
            entry.RawBody.Length,
            BuildRawBodyPreview(entry.RawBody));

    private static AdoWebhookEventDetailDto ToDetailDto(AdoWebhookEventEntry entry) =>
        new(
            entry.Id,
            entry.ReceivedAt,
            entry.RawBody.Length,
            entry.RawBody);

    private static string BuildRawBodyPreview(string rawBody)
    {
        if (string.IsNullOrEmpty(rawBody))
        {
            return string.Empty;
        }

        return rawBody.Length <= AdoWebhookPreviewLength
            ? rawBody
            : rawBody[..AdoWebhookPreviewLength];
    }

    private static AdminAnnouncementResponse ToAdminResponse(SiteAnnouncement entity) =>
        new(
            entity.Id,
            entity.Message,
            entity.Severity,
            entity.Scope,
            entity.SortOrder,
            entity.IsEnabled,
            entity.StartsAt,
            entity.EndsAt,
            entity.LinkUrl,
            entity.LinkLabel,
            entity.CreatedAt,
            entity.CreatedByUserId,
            entity.UpdatedAt,
            entity.UpdatedByUserId);

    private static async Task<bool> IsAdminAsync(
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        CancellationToken ct)
    {
        var userId = currentUser.GetRequiredUserId();
        return await dbContext.AspNetUsers
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId && x.Role == AdminRoleCode, ct);
    }
}
