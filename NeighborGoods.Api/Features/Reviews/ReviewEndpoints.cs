using NeighborGoods.Api.Features.Reviews.Contracts;
using NeighborGoods.Api.Features.Reviews.Services;
using NeighborGoods.Api.Shared.ApiContracts;
using NeighborGoods.Api.Shared.Security;

namespace NeighborGoods.Api.Features.Reviews;

public static class ReviewEndpoints
{
    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/purchase-requests/{requestId:guid}/review-status", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            ReviewService service,
            Guid requestId,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await service.GetStatusAsync(userId, requestId, ct);
            if (data is null)
            {
                return ToErrorResult(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("GetPurchaseRequestReviewStatusV1")
        .RequireAuthorization();

        app.MapPost("/api/v1/purchase-requests/{requestId:guid}/reviews", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            ReviewService service,
            Guid requestId,
            CreateReviewRequest request,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await service.CreateAsync(userId, requestId, request, ct);
            if (data is null)
            {
                return ToErrorResult(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("CreatePurchaseRequestReviewV1")
        .RequireAuthorization();

        return app;
    }

    private static IResult ToErrorResult(HttpContext httpContext, string code, string message)
    {
        var statusCode = code switch
        {
            "PURCHASE_REQUEST_NOT_FOUND" => StatusCodes.Status404NotFound,
            "PURCHASE_REQUEST_ACCESS_DENIED" => StatusCodes.Status403Forbidden,
            "REVIEW_ALREADY_EXISTS" => StatusCodes.Status409Conflict,
            "REVIEW_NOT_AVAILABLE" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Json(
            ApiResponseFactory.Error(code, message, httpContext),
            statusCode: statusCode);
    }
}
