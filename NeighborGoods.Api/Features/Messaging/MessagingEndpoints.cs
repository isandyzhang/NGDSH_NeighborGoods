using NeighborGoods.Api.Features.Messaging.Contracts.Requests;
using NeighborGoods.Api.Features.Messaging.Services;
using NeighborGoods.Api.Features.PurchaseRequests;
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

        app.MapGet("/api/v1/conversations/unread-summary", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            MessagingQueryService queryService,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var totalUnread = await queryService.GetTotalUnreadCountAsync(userId, ct);
            return Results.Ok(ApiResponseFactory.Success(new { totalUnread }, httpContext));
        })
        .WithName("GetConversationsUnreadSummaryV1")
        .RequireAuthorization();

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

        app.MapPost("/api/v1/conversations/{conversationId:guid}/messages/share-line-contact", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            MessagingCommandService commandService,
            Guid conversationId,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (message, errorCode, errorMessage) = await commandService.ShareLineContactAsync(
                userId,
                conversationId,
                ct);

            if (errorCode is not null)
            {
                return MessagingError(httpContext, errorCode, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(message, httpContext));
        })
        .WithName("ShareLineContactMessageV1")
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
                // 對話尚未建立交易請求屬於正常狀態，回 200 + null，避免前端每次進聊天室都出現 404 噪音。
                if (string.Equals(errorCode, "PURCHASE_REQUEST_NOT_FOUND", StringComparison.Ordinal))
                {
                    return Results.Ok(ApiResponseFactory.Success<object?>(null, httpContext));
                }
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

        app.MapPost("/api/v1/conversations/{conversationId:guid}/purchase-request/complete-by-seller", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            PurchaseRequestService service,
            Guid conversationId,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await service.CompleteBySellerByConversationAsync(
                userId,
                conversationId,
                ct);
            if (data is null)
            {
                return MessagingError(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("CompleteConversationPurchaseRequestBySellerV1")
        .WithSummary("賣家在對話中標記完成交易")
        .RequireAuthorization()
        .RequireRateLimiting("MessagingWrite");

        app.MapPost("/api/v1/conversations/{conversationId:guid}/purchase-request/confirm-received", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            PurchaseRequestService service,
            Guid conversationId,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await service.ConfirmReceivedByBuyerByConversationAsync(
                userId,
                conversationId,
                ct);
            if (data is null)
            {
                return MessagingError(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("ConfirmConversationPurchaseRequestReceivedV1")
        .WithSummary("買家在對話中確認收貨")
        .RequireAuthorization()
        .RequireRateLimiting("MessagingWrite");

        return app;
    }

    private static IResult MessagingError(HttpContext httpContext, string code, string message)
    {
        var body = ApiResponseFactory.Error(code, message, httpContext);
        var statusCode = PurchaseRequestErrorHttpMapper.ToStatusCode(code);

        return Results.Json(body, statusCode: statusCode);
    }
}
