using NeighborGoods.Api.Features.Messaging.Contracts.Requests;
using NeighborGoods.Api.Features.Messaging.Services;
using NeighborGoods.Api.Features.PurchaseRequests.Contracts.Requests;
using NeighborGoods.Api.Features.PurchaseRequests.Services;
using NeighborGoods.Api.Shared.ApiContracts;
using NeighborGoods.Api.Shared.Security;

namespace NeighborGoods.Api.Features.Messaging;

public static class MessagingEndpoints
{
    public static IEndpointRouteBuilder MapMessagingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/conversations", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            MessagingCommandService commandService,
            EnsureConversationRequest request,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (conversationId, errorCode, errorMessage) = await commandService.EnsureConversationAsync(
                userId,
                request.ListingId,
                request.OtherUserId,
                ct);

            if (errorCode is not null)
            {
                return MessagingError(httpContext, errorCode, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(new { conversationId }, httpContext));
        })
        .WithName("EnsureConversationV1")
        .RequireAuthorization()
        .RequireRateLimiting("MessagingWrite");

        app.MapGet("/api/v1/conversations", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            MessagingQueryService queryService,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var items = await queryService.ListConversationsAsync(userId, ct);
            return Results.Ok(ApiResponseFactory.Success(new { items }, httpContext));
        })
        .WithName("ListConversationsV1")
        .RequireAuthorization();

        app.MapGet("/api/v1/conversations/{conversationId:guid}/messages", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            MessagingQueryService queryService,
            Guid conversationId,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await queryService.GetMessagesPageAsync(
                conversationId,
                userId,
                page,
                pageSize,
                ct);

            if (errorCode is not null)
            {
                return MessagingError(httpContext, errorCode, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("GetConversationMessagesV1")
        .RequireAuthorization();

        app.MapPost("/api/v1/conversations/{conversationId:guid}/messages", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            MessagingCommandService commandService,
            Guid conversationId,
            SendMessageRequest request,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (message, errorCode, errorMessage) = await commandService.SendMessageAsync(
                userId,
                conversationId,
                request.Content ?? string.Empty,
                ct);

            if (errorCode is not null)
            {
                return MessagingError(httpContext, errorCode, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(message, httpContext));
        })
        .WithName("SendConversationMessageV1")
        .RequireAuthorization()
        .RequireRateLimiting("MessagingWrite");

        app.MapPost("/api/v1/conversations/{conversationId:guid}/read", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            MessagingCommandService commandService,
            Guid conversationId,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (ok, errorCode, errorMessage) = await commandService.MarkReadAsync(userId, conversationId, ct);
            if (!ok)
            {
                return MessagingError(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(new { read = true }, httpContext));
        })
        .WithName("MarkConversationReadV1")
        .RequireAuthorization()
        .RequireRateLimiting("MessagingWrite");

        app.MapGet("/api/v1/conversations/{conversationId:guid}/purchase-request/current", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            PurchaseRequestService service,
            Guid conversationId,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await service.GetCurrentByConversationAsync(
                userId,
                conversationId,
                ct);
            if (data is null)
            {
                return MessagingError(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("GetCurrentConversationPurchaseRequestV1")
        .WithSummary("取得對話目前交易請求")
        .WithDescription("回傳最新交易請求狀態與倒數秒數（UTC）。若請求已逾時，會先轉為 Expired。")
        .RequireAuthorization();

        app.MapPost("/api/v1/conversations/{conversationId:guid}/purchase-request/accept", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            PurchaseRequestService service,
            Guid conversationId,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await service.AcceptByConversationAsync(
                userId,
                conversationId,
                ct);
            if (data is null)
            {
                return MessagingError(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("AcceptConversationPurchaseRequestV1")
        .WithSummary("在對話中同意交易請求")
        .RequireAuthorization()
        .RequireRateLimiting("MessagingWrite");

        app.MapPost("/api/v1/conversations/{conversationId:guid}/purchase-request/reject", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            PurchaseRequestService service,
            Guid conversationId,
            RejectPurchaseRequestRequest request,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await service.RejectByConversationAsync(
                userId,
                conversationId,
                request.Reason,
                ct);
            if (data is null)
            {
                return MessagingError(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("RejectConversationPurchaseRequestV1")
        .WithSummary("在對話中拒絕交易請求")
        .RequireAuthorization()
        .RequireRateLimiting("MessagingWrite");

        app.MapPost("/api/v1/conversations/{conversationId:guid}/purchase-request/cancel", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            PurchaseRequestService service,
            Guid conversationId,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await service.CancelByConversationAsync(
                userId,
                conversationId,
                ct);
            if (data is null)
            {
                return MessagingError(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("CancelConversationPurchaseRequestV1")
        .WithSummary("在對話中取消交易請求")
        .RequireAuthorization()
        .RequireRateLimiting("MessagingWrite");

        return app;
    }

    private static IResult MessagingError(HttpContext httpContext, string code, string message)
    {
        var body = ApiResponseFactory.Error(code, message, httpContext);
        var statusCode = code switch
        {
            "CONVERSATION_ACCESS_DENIED" => StatusCodes.Status403Forbidden,
            "PURCHASE_REQUEST_ACCESS_DENIED" => StatusCodes.Status403Forbidden,
            "CONVERSATION_NOT_FOUND" or "LISTING_NOT_FOUND" or "PURCHASE_REQUEST_NOT_FOUND" => StatusCodes.Status404NotFound,
            "PURCHASE_REQUEST_ALREADY_PENDING" or "PURCHASE_REQUEST_NOT_PENDING" or "PURCHASE_REQUEST_EXPIRED"
                or "LISTING_NOT_AVAILABLE" or "LISTING_INVALID_STATUS_TRANSITION" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Json(body, statusCode: statusCode);
    }
}
