using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Data;
using global::System.Security.Claims;

namespace NeighborGoods.Messaging.Hubs;

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

    public async Task LeaveConversation(Guid conversationId)
    {
        var userId = Context.UserIdentifier
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new HubException("UNAUTHORIZED");
        }

        var canLeave = await dbContext.Conversations
            .AsNoTracking()
            .AnyAsync(
                c => c.Id == conversationId &&
                     (c.Participant1Id == userId || c.Participant2Id == userId));
        if (!canLeave)
        {
            throw new HubException("CONVERSATION_ACCESS_DENIED");
        }

        var groupName = ConversationGroupName(conversationId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}
