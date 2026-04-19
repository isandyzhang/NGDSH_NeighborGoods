using NeighborGoods.Api.Features.Messaging.Contracts.Requests;
using NeighborGoods.Api.Features.Messaging.Services;
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

        return app;
    }

    private static IResult MessagingError(HttpContext httpContext, string code, string message)
    {
        var body = ApiResponseFactory.Error(code, message, httpContext);
        var statusCode = code switch
        {
            "CONVERSATION_ACCESS_DENIED" => StatusCodes.Status403Forbidden,
            "CONVERSATION_NOT_FOUND" or "LISTING_NOT_FOUND" => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Json(body, statusCode: statusCode);
    }
}
