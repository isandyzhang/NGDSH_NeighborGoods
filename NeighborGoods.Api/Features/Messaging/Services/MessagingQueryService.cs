using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Messaging.Contracts.Responses;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Features.Messaging.Services;

public sealed class MessagingQueryService(NeighborGoodsDbContext dbContext)
{
    public async Task<IReadOnlyList<ConversationListItemDto>> ListConversationsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var conversations = await dbContext.Conversations
            .AsNoTracking()
            .Include(c => c.Participant1)
            .Include(c => c.Participant2)
            .Include(c => c.Listing)
            .Where(c => c.Participant1Id == userId || c.Participant2Id == userId)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(cancellationToken);

        if (conversations.Count == 0)
        {
            return Array.Empty<ConversationListItemDto>();
        }

        var conversationIds = conversations.Select(c => c.Id).ToList();

        var lastMessages = await dbContext.Messages
            .AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => new
            {
                ConversationId = g.Key,
                Last = g.OrderByDescending(m => m.CreatedAt).First()
            })
            .ToDictionaryAsync(x => x.ConversationId, x => x.Last, cancellationToken);

        var unreadCounts1 = await (
            from c in dbContext.Conversations.AsNoTracking()
            where conversationIds.Contains(c.Id) && c.Participant1Id == userId
            from m in dbContext.Messages.AsNoTracking()
            where m.ConversationId == c.Id
                && m.SenderId != userId
                && (c.Participant1LastReadAt == null || m.CreatedAt > c.Participant1LastReadAt.Value)
            group m by c.Id
            into g
            select new { ConversationId = g.Key, UnreadCount = g.Count() }
        ).ToDictionaryAsync(x => x.ConversationId, x => x.UnreadCount, cancellationToken);

        var unreadCounts2 = await (
            from c in dbContext.Conversations.AsNoTracking()
            where conversationIds.Contains(c.Id) && c.Participant2Id == userId
            from m in dbContext.Messages.AsNoTracking()
            where m.ConversationId == c.Id
                && m.SenderId != userId
                && (c.Participant2LastReadAt == null || m.CreatedAt > c.Participant2LastReadAt.Value)
            group m by c.Id
            into g
            select new { ConversationId = g.Key, UnreadCount = g.Count() }
        ).ToDictionaryAsync(x => x.ConversationId, x => x.UnreadCount, cancellationToken);

        var unreadCountDict = new Dictionary<Guid, int>();
        foreach (var kv in unreadCounts1)
        {
            unreadCountDict[kv.Key] = kv.Value;
        }

        foreach (var kv in unreadCounts2)
        {
            unreadCountDict[kv.Key] = kv.Value;
        }

        var items = new List<ConversationListItemDto>(conversations.Count);
        foreach (var c in conversations)
        {
            var other = c.Participant1Id == userId ? c.Participant2 : c.Participant1;
            if (other is null)
            {
                continue;
            }

            lastMessages.TryGetValue(c.Id, out var lastMessage);
            unreadCountDict.TryGetValue(c.Id, out var unread);

            var title = c.Listing?.Title ?? "未知商品";

            items.Add(new ConversationListItemDto
            {
                ConversationId = c.Id,
                ListingId = c.ListingId,
                ListingTitle = title,
                OtherUserId = other.Id,
                OtherDisplayName = other.DisplayName,
                LastMessagePreview = lastMessage?.Content,
                LastMessageAt = lastMessage?.CreatedAt,
                UnreadCount = unread
            });
        }

        return items;
    }

    public async Task<(MessagesPageResponse? Data, string? ErrorCode, string? ErrorMessage)> GetMessagesPageAsync(
        Guid conversationId,
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        if (conversation is null)
        {
            return (null, "CONVERSATION_NOT_FOUND", "找不到對話");
        }

        if (conversation.Participant1Id != userId && conversation.Participant2Id != userId)
        {
            return (null, "CONVERSATION_ACCESS_DENIED", "無權限訪問此對話");
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var totalCount = await dbContext.Messages
            .AsNoTracking()
            .CountAsync(m => m.ConversationId == conversationId, cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var skip = Math.Max(0, totalCount - page * pageSize);

        var slice = await dbContext.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .Include(m => m.Sender)
            .ToListAsync(cancellationToken);

        var dtos = slice.Select(m => new MessageItemDto
        {
            Id = m.Id,
            SenderId = m.SenderId,
            SenderDisplayName = m.Sender?.DisplayName ?? "未知用戶",
            Content = m.Content,
            CreatedAt = m.CreatedAt
        }).ToList();

        return (new MessagesPageResponse
        {
            Items = dtos,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        }, null, null);
    }
}
