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
        string? q = null,
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

        var query = dbContext.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            query = query.Where(m => EF.Functions.Like(m.Content, $"%{keyword}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);

        var slice = await query
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

    public async Task<AdminConversationByListingResponse> ListByListingAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        var listingGroupQuery = dbContext.Conversations
            .AsNoTracking()
            .GroupBy(c => c.ListingId)
            .Select(g => new
            {
                ListingId = g.Key,
                ConversationCount = g.Count(),
                LastUpdatedAt = g.Max(x => x.UpdatedAt)
            });

        var totalCount = await listingGroupQuery.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        var listingGroups = await listingGroupQuery
            .OrderByDescending(x => x.LastUpdatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        if (listingGroups.Count == 0)
        {
            return new AdminConversationByListingResponse(Array.Empty<AdminConversationByListingItemDto>(), normalizedPage, normalizedPageSize, totalCount, totalPages);
        }

        var listingIds = listingGroups.Select(x => x.ListingId).ToList();
        var listings = await dbContext.Listings
            .AsNoTracking()
            .Where(x => listingIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Title,
                SellerDisplayName = x.Seller.DisplayName,
                FirstImage = x.ListingImages
                    .OrderBy(img => img.SortOrder)
                    .Select(img => img.ImageUrl)
                    .FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var conversations = await dbContext.Conversations
            .AsNoTracking()
            .Include(c => c.Participant1)
            .Include(c => c.Participant2)
            .Where(c => listingIds.Contains(c.ListingId))
            .ToListAsync(cancellationToken);

        var conversationIds = conversations.Select(c => c.Id).ToList();
        var messageStats = await dbContext.Messages
            .AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => new
            {
                ConversationId = g.Key,
                Count = g.Count(),
                LastMessageAt = g.Max(x => x.CreatedAt)
            })
            .ToDictionaryAsync(x => x.ConversationId, cancellationToken);

        var groupedConversations = conversations
            .GroupBy(c => c.ListingId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<AdminConversationByListingConversationItemDto>)g
                    .OrderByDescending(x => x.UpdatedAt)
                    .Select(x =>
                    {
                        messageStats.TryGetValue(x.Id, out var stats);
                        return new AdminConversationByListingConversationItemDto(
                            x.Id,
                            x.Participant1Id,
                            x.Participant1?.DisplayName ?? "未知用戶",
                            x.Participant2Id,
                            x.Participant2?.DisplayName ?? "未知用戶",
                            stats?.Count ?? 0,
                            stats?.LastMessageAt);
                    })
                    .ToList());

        var items = listingGroups.Select(group =>
        {
            listings.TryGetValue(group.ListingId, out var listing);
            groupedConversations.TryGetValue(group.ListingId, out var conversationItems);
            return new AdminConversationByListingItemDto(
                group.ListingId,
                listing?.Title ?? "未知商品",
                listing?.SellerDisplayName ?? "未知賣家",
                listing?.FirstImage,
                group.ConversationCount,
                group.LastUpdatedAt,
                conversationItems ?? Array.Empty<AdminConversationByListingConversationItemDto>());
        }).ToList();

        return new AdminConversationByListingResponse(items, normalizedPage, normalizedPageSize, totalCount, totalPages);
    }
}
