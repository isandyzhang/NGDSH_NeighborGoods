using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Shared.Persistence;
using global::System.Security.Claims;

namespace NeighborGoods.Api.Features.Messaging;

[Authorize]
public sealed class MessageHub(
    NeighborGoodsDbContext dbContext) : Hub
{
    public static string ConversationGroupName(Guid conversationId) => $"conversation:{conversationId:N}";

    public async Task JoinConversation(Guid conversationId)
    {
        var userId = Context.UserIdentifier
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new HubException("UNAUTHORIZED");
        }

        var canJoin = await dbContext.Conversations
            .AsNoTracking()
            .AnyAsync(
                c => c.Id == conversationId &&
                     (c.Participant1Id == userId || c.Participant2Id == userId));
        if (!canJoin)
        {
            throw new HubException("CONVERSATION_ACCESS_DENIED");
        }

        var groupName = ConversationGroupName(conversationId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }
}
