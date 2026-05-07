using NeighborGoods.Api.Features.PurchaseRequests.Contracts.Requests;
using NeighborGoods.Api.Features.PurchaseRequests.Services;
using NeighborGoods.Api.Shared.ApiContracts;
using NeighborGoods.Api.Shared.Security;

namespace NeighborGoods.Api.Features.PurchaseRequests;

public static class PurchaseRequestEndpoints
{
    public static IEndpointRouteBuilder MapPurchaseRequestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/listings/{id:guid}/purchase-requests", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            PurchaseRequestService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await service.CreateAsync(userId, id, ct);
            if (data is null)
            {
                return ToErrorResult(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("CreatePurchaseRequestV1")
        .RequireAuthorization();

        app.MapPost("/api/v1/purchase-requests/{requestId:guid}/accept", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            PurchaseRequestService service,
            Guid requestId,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await service.AcceptAsync(userId, requestId, ct);
            if (data is null)
            {
                return ToErrorResult(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("AcceptPurchaseRequestV1")
        .RequireAuthorization();

        app.MapPost("/api/v1/purchase-requests/{requestId:guid}/reject", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            PurchaseRequestService service,
            Guid requestId,
            RejectPurchaseRequestRequest request,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await service.RejectAsync(
                userId,
                requestId,
                request.Reason,
                ct);
            if (data is null)
            {
                return ToErrorResult(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("RejectPurchaseRequestV1")
        .RequireAuthorization();

        app.MapPost("/api/v1/purchase-requests/{requestId:guid}/cancel", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            PurchaseRequestService service,
            Guid requestId,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await service.CancelAsync(userId, requestId, ct);
            if (data is null)
            {
                return ToErrorResult(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("CancelPurchaseRequestV1")
        .RequireAuthorization();

        app.MapGet("/api/v1/purchase-requests/{requestId:guid}", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            PurchaseRequestService service,
            Guid requestId,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await service.GetByIdAsync(userId, requestId, ct);
            if (data is null)
            {
                return ToErrorResult(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("GetPurchaseRequestV1")
        .RequireAuthorization();

        return app;
    }

    private static IResult ToErrorResult(HttpContext httpContext, string code, string message)
    {
        var statusCode = PurchaseRequestErrorHttpMapper.ToStatusCode(code);

        return Results.Json(
            ApiResponseFactory.Error(code, message, httpContext),
            statusCode: statusCode);
    }
}
