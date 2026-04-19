using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Auth.Contracts.Requests;
using NeighborGoods.Api.Features.Auth.Contracts.Responses;
using NeighborGoods.Api.Features.Auth.Services;
using NeighborGoods.Api.Shared.ApiContracts;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/login", async (
            HttpContext httpContext,
            LoginRequest request,
            PasswordAuthService passwordAuthService,
            ITokenService tokenService,
            CancellationToken ct = default) =>
        {
            var user = await passwordAuthService.ValidateCredentialsAsync(request.UserNameOrEmail, request.Password, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var tokens = await tokenService.IssueAsync(user, ct);
            var response = ToTokenResponse(tokens);
            return Results.Ok(ApiResponseFactory.Success(response, httpContext));
        })
        .WithName("LoginV1")
        .RequireRateLimiting("AuthWrite");

        app.MapPost("/api/v1/auth/refresh", async (
            HttpContext httpContext,
            RefreshTokenRequest request,
            ITokenService tokenService,
            CancellationToken ct = default) =>
        {
            var tokens = await tokenService.RefreshAsync(request.RefreshToken, ct);
            if (tokens is null)
            {
                return Results.Unauthorized();
            }

            var response = ToTokenResponse(tokens);
            return Results.Ok(ApiResponseFactory.Success(response, httpContext));
        })
        .WithName("RefreshTokenV1")
        .RequireRateLimiting("AuthWrite");

        app.MapPost("/api/v1/auth/revoke", async (
            HttpContext httpContext,
            RevokeTokenRequest request,
            ITokenService tokenService,
            CancellationToken ct = default) =>
        {
            var revoked = await tokenService.RevokeAsync(request.RefreshToken, ct);
            if (!revoked)
            {
                return Results.BadRequest(ApiResponseFactory.Error("INVALID_REFRESH_TOKEN", "Refresh token 無效或已失效", httpContext));
            }

            return Results.Ok(ApiResponseFactory.Success(new { revoked = true }, httpContext));
        })
        .WithName("RevokeTokenV1")
        .RequireRateLimiting("AuthWrite");

        app.MapGet("/api/v1/auth/line/login", (
            ILineOAuthStateStore stateStore,
            ILineOAuthClient lineOAuthClient) =>
        {
            var state = stateStore.Create();
            var redirectUrl = lineOAuthClient.BuildAuthorizeUrl(state);
            return Results.Redirect(redirectUrl);
        })
        .WithName("LineLoginV1");

        app.MapGet("/api/v1/auth/line/callback", async (
            HttpContext httpContext,
            string code,
            string state,
            ILineOAuthStateStore stateStore,
            ILineOAuthClient lineOAuthClient,
            NeighborGoodsDbContext dbContext,
            ITokenService tokenService,
            CancellationToken ct = default) =>
        {
            if (!stateStore.Consume(state))
            {
                return Results.BadRequest(ApiResponseFactory.Error("INVALID_LINE_STATE", "LINE 驗證 state 無效或過期", httpContext));
            }

            var profile = await lineOAuthClient.ExchangeCodeAsync(code, ct);
            if (profile is null)
            {
                return Results.BadRequest(ApiResponseFactory.Error("LINE_EXCHANGE_FAILED", "LINE 驗證失敗", httpContext));
            }

            var user = await dbContext.AspNetUsers
                .FirstOrDefaultAsync(x => x.LineUserId == profile.Subject, ct);
            if (user is null)
            {
                user = CreateLineUser(profile);
                dbContext.AspNetUsers.Add(user);
                await dbContext.SaveChangesAsync(ct);
            }

            var tokens = await tokenService.IssueAsync(user, ct);
            var response = ToTokenResponse(tokens);
            return Results.Ok(ApiResponseFactory.Success(response, httpContext));
        })
        .WithName("LineCallbackV1");

        return app;
    }

    private static AspNetUser CreateLineUser(LineOAuthProfile profile)
    {
        var normalizedName = $"LINE_{profile.Subject}".ToUpperInvariant();
        return new AspNetUser
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? "LINE 使用者" : profile.DisplayName,
            LineUserId = profile.Subject,
            Role = 0,
            CreatedAt = DateTime.UtcNow,
            UserName = $"line_{profile.Subject}",
            NormalizedUserName = normalizedName,
            Email = null,
            NormalizedEmail = null,
            EmailConfirmed = false,
            PasswordHash = null,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            PhoneNumberConfirmed = false,
            TwoFactorEnabled = false,
            LockoutEnabled = false,
            AccessFailedCount = 0,
            LineNotificationPreference = 0,
            EmailNotificationEnabled = false,
            TopPinCredits = 0
        };
    }

    private static AuthTokenResponse ToTokenResponse(AuthTokenPair pair)
    {
        return new AuthTokenResponse(
            pair.AccessToken,
            pair.AccessTokenExpiresAt,
            pair.RefreshToken,
            pair.RefreshTokenExpiresAt,
            pair.UserId);
    }
}
