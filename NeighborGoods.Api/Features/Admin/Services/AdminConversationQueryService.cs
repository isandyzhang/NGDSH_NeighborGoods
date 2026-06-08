using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Admin.Contracts.Responses;
using NeighborGoods.Api.Features.Messaging.Contracts.Responses;

namespace NeighborGoods.Api.Features.Admin.Services;

public sealed class AdminConversationQueryService(NeighborGoodsDbContext dbContext)
{
    public async Task<AdminConversationListResponse> ListConversationsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        var totalCount = await dbContext.Conversations.AsNoTracking().CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);

        var conversations = await dbContext.Conversations
            .AsNoTracking()
            .Include(c => c.Participant1)
            .Include(c => c.Participant2)
            .Include(c => c.Listing)
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        if (conversations.Count == 0)
        {
            return new AdminConversationListResponse(
                Array.Empty<AdminConversationListItemDto>(),
                normalizedPage,
                normalizedPageSize,
                totalCount,
                totalPages);
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

        var messageCounts = await dbContext.Messages
            .AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ConversationId, x => x.Count, cancellationToken);

        var items = conversations.Select(c =>
        {
            lastMessages.TryGetValue(c.Id, out var lastMessage);
            messageCounts.TryGetValue(c.Id, out var messageCount);

            return new AdminConversationListItemDto(
                c.Id,
                c.ListingId,
                c.Listing?.Title ?? "未知商品",
                c.Participant1Id,
                c.Participant1?.DisplayName ?? "未知用戶",
                c.Participant2Id,
                c.Participant2?.DisplayName ?? "未知用戶",
                lastMessage?.Content,
                lastMessage?.CreatedAt,
                c.UpdatedAt,
                messageCount);
        }).ToList();

        return new AdminConversationListResponse(
            items,
            normalizedPage,
            normalizedPageSize,
            totalCount,
            totalPages);
    }

    public async Task<(MessagesPageResponse? Data, bool ConversationExists)> GetMessagesAsync(
        Guid conversationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.Conversations
            .AsNoTracking()
            .AnyAsync(c => c.Id == conversationId, cancellationToken);

        if (!exists)
        {
            return (null, false);
        }

        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        var totalCount = await dbContext.Messages
            .AsNoTracking()
            .CountAsync(m => m.ConversationId == conversationId, cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);

        var slice = await dbContext.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Include(m => m.Sender)
            .ToListAsync(cancellationToken);

        var items = slice.Select(m => new MessageItemDto
        {
            Id = m.Id,
            ConversationId = conversationId,
            SenderId = m.SenderId,
            SenderDisplayName = m.Sender?.DisplayName ?? "未知用戶",
            Content = m.Content,
            CreatedAt = m.CreatedAt
        }).ToList();

        return (new MessagesPageResponse
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        }, true);
    }
}
