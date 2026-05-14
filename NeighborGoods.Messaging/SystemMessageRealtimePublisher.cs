using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Data;
using NeighborGoods.Data.LegacyEntities;
using NeighborGoods.Messaging.Contracts;
using NeighborGoods.Messaging.Hubs;

namespace NeighborGoods.Messaging;

public interface ISystemMessageRealtimePublisher
{
    Task PublishLatestSystemMessageAsync(Guid conversationId, CancellationToken cancellationToken);

    Task PublishLatestSystemMessagesAsync(
        IEnumerable<Guid> conversationIds,
        CancellationToken cancellationToken);
}

public sealed class SystemMessageRealtimePublisher(
    NeighborGoodsDbContext dbContext,
    IHubContext<MessageHub> hubContext) : ISystemMessageRealtimePublisher
{
    public async Task PublishLatestSystemMessageAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var message = await dbContext.Messages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId && x.Content.StartsWith("[系統發送]"))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (message is null)
        {
            return;
        }

        var participants = await dbContext.Conversations
            .AsNoTracking()
            .Where(x => x.Id == conversationId)
            .Select(x => new { x.Participant1Id, x.Participant2Id })
            .FirstOrDefaultAsync(cancellationToken);
        if (participants is null)
        {
            return;
        }

        var senderDisplayName = await dbContext.AspNetUsers
            .AsNoTracking()
            .Where(x => x.Id == message.SenderId)
            .Select(x => x.DisplayName)
            .FirstOrDefaultAsync(cancellationToken)
            ?? message.SenderId;

        var dto = new MessageItemDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderDisplayName = senderDisplayName,
            Content = message.Content,
            CreatedAt = message.CreatedAt
        };

        await hubContext.Clients.Users(participants.Participant1Id, participants.Participant2Id)
            .SendAsync("ReceiveMessage", dto, cancellationToken);
        await hubContext.Clients.Group(MessageHub.ConversationGroupName(conversationId))
            .SendAsync("ReceiveMessage", dto, cancellationToken);
    }

    public async Task PublishLatestSystemMessagesAsync(
        IEnumerable<Guid> conversationIds,
        CancellationToken cancellationToken)
    {
        var ids = conversationIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var messages = await dbContext.Messages
            .AsNoTracking()
            .Where(x => ids.Contains(x.ConversationId) && x.Content.StartsWith("[系統發送]"))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var latestByConversationId = new Dictionary<Guid, Message>();
        foreach (var message in messages)
        {
            if (!latestByConversationId.ContainsKey(message.ConversationId))
            {
                latestByConversationId.Add(message.ConversationId, message);
            }
        }

        if (latestByConversationId.Count == 0)
        {
            return;
        }

        var participantsByConversationId = await dbContext.Conversations
            .AsNoTracking()
            .Where(x => latestByConversationId.Keys.Contains(x.Id))
            .Select(x => new { x.Id, x.Participant1Id, x.Participant2Id })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var senderIds = latestByConversationId.Values
            .Select(x => x.SenderId)
            .Distinct()
            .ToList();
        var senderNamesById = await dbContext.AspNetUsers
            .AsNoTracking()
            .Where(x => senderIds.Contains(x.Id))
            .Select(x => new { x.Id, x.DisplayName })
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        foreach (var (conversationId, message) in latestByConversationId)
        {
            if (!participantsByConversationId.TryGetValue(conversationId, out var participants))
            {
                continue;
            }

            var senderDisplayName = senderNamesById.TryGetValue(message.SenderId, out var displayName)
                ? displayName
                : message.SenderId;
            var dto = new MessageItemDto
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                SenderDisplayName = senderDisplayName ?? message.SenderId,
                Content = message.Content,
                CreatedAt = message.CreatedAt
            };

            await hubContext.Clients.Users(participants.Participant1Id, participants.Participant2Id)
                .SendAsync("ReceiveMessage", dto, cancellationToken);
            await hubContext.Clients.Group(MessageHub.ConversationGroupName(conversationId))
                .SendAsync("ReceiveMessage", dto, cancellationToken);
        }
    }
}
