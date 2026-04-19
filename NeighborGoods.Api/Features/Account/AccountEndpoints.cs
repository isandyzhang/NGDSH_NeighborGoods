using NeighborGoods.Api.Features.Account.Contracts.Requests;
using NeighborGoods.Api.Features.Account.Contracts.Responses;
using NeighborGoods.Api.Features.Account.Services;
using NeighborGoods.Api.Shared.ApiContracts;
using NeighborGoods.Api.Shared.Security;

namespace NeighborGoods.Api.Features.Account;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/account/register/send-code", async (
            HttpContext httpContext,
            SendVerificationCodeRequest request,
            AccountRegistrationService registrationService,
            CancellationToken ct = default) =>
        {
            var (ok, errorCode, errorMessage) = await registrationService.SendRegisterVerificationCodeAsync(
                request.Email,
                ct);
            if (!ok)
            {
                return AccountError(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(new { sent = true }, httpContext));
        })
        .WithName("AccountRegisterSendCodeV1")
        .RequireRateLimiting("AccountSendCode");

        app.MapPost("/api/v1/account/register", async (
            HttpContext httpContext,
            RegisterAccountRequest request,
            AccountRegistrationService registrationService,
            CancellationToken ct = default) =>
        {
            var (tokens, errorCode, errorMessage) = await registrationService.RegisterAsync(request, ct);
            if (tokens is null)
            {
                return AccountError(httpContext, errorCode!, errorMessage!);
            }

            var response = new RegisterAccountResponse(
                tokens.AccessToken,
                tokens.AccessTokenExpiresAt,
                tokens.RefreshToken,
                tokens.RefreshTokenExpiresAt,
                tokens.UserId);
            return Results.Ok(ApiResponseFactory.Success(response, httpContext));
        })
        .WithName("AccountRegisterV1")
        .RequireRateLimiting("AccountWrite");

        app.MapPost("/api/v1/account/email/send-code", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            SendVerificationCodeRequest request,
            AccountEmailVerificationService verificationService,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (ok, errorCode, errorMessage) = await verificationService.SendListingVerificationCodeAsync(
                userId,
                request.Email,
                ct);
            if (!ok)
            {
                return AccountError(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(new { sent = true }, httpContext));
        })
        .WithName("AccountEmailSendCodeV1")
        .RequireAuthorization()
        .RequireRateLimiting("AccountSendCode");

        app.MapPost("/api/v1/account/email/verify", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            VerifyEmailCodeRequest request,
            AccountEmailVerificationService verificationService,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (ok, errorCode, errorMessage) = await verificationService.VerifyListingEmailCodeAsync(
                userId,
                request,
                ct);
            if (!ok)
            {
                return AccountError(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(new { verified = true }, httpContext));
        })
        .WithName("AccountEmailVerifyV1")
        .RequireAuthorization()
        .RequireRateLimiting("AccountWrite");

        app.MapGet("/api/v1/account/me", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            AccountProfileService profileService,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await profileService.GetMeAsync(userId, ct);
            if (data is null)
            {
                return AccountError(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("AccountGetMeV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/account/me", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            UpdateProfileRequest request,
            AccountProfileService profileService,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (ok, errorCode, errorMessage) = await profileService.UpdateDisplayNameAsync(
                userId,
                request.DisplayName,
                ct);
            if (!ok)
            {
                return AccountError(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(new { updated = true }, httpContext));
        })
        .WithName("AccountUpdateMeV1")
        .RequireAuthorization()
        .RequireRateLimiting("AccountWrite");

        app.MapPost("/api/v1/account/line/bind/start", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            AccountLineBindingService lineBindingService,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await lineBindingService.StartAsync(userId, ct);
            if (data is null)
            {
                return AccountError(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("AccountLineBindStartV1")
        .RequireAuthorization()
        .RequireRateLimiting("AccountWrite");

        app.MapGet("/api/v1/account/line/bind/status", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            AccountLineBindingService lineBindingService,
            Guid pendingBindingId,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await lineBindingService.GetStatusAsync(userId, pendingBindingId, ct);
            if (data is null)
            {
                return AccountError(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("AccountLineBindStatusV1")
        .RequireAuthorization();

        app.MapPost("/api/v1/account/line/bind/confirm", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            ConfirmLineBindingRequest request,
            AccountLineBindingService lineBindingService,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (ok, errorCode, errorMessage) = await lineBindingService.ConfirmAsync(userId, request.PendingBindingId, ct);
            if (!ok)
            {
                return AccountError(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(new { bound = true }, httpContext));
        })
        .WithName("AccountLineBindConfirmV1")
        .RequireAuthorization()
        .RequireRateLimiting("AccountWrite");

        app.MapPost("/api/v1/account/line/bind/unbind", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            AccountLineBindingService lineBindingService,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (ok, errorCode, errorMessage) = await lineBindingService.UnbindAsync(userId, ct);
            if (!ok)
            {
                return AccountError(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(new { unbound = true }, httpContext));
        })
        .WithName("AccountLineBindUnbindV1")
        .RequireAuthorization()
        .RequireRateLimiting("AccountWrite");

        return app;
    }

    private static IResult AccountError(HttpContext httpContext, string code, string message)
    {
        var statusCode = code switch
        {
            "USER_NOT_FOUND" => StatusCodes.Status404NotFound,
            "EMAIL_NOT_CONFIGURED" => StatusCodes.Status503ServiceUnavailable,
            "LINE_BIND_PENDING_NOT_FOUND" => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Json(
            ApiResponseFactory.Error(code, message, httpContext),
            statusCode: statusCode);
    }
}
