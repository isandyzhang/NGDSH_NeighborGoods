using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Listing;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Features.Integrations.Line.Services;

public sealed class LineMenuQueryService(NeighborGoodsDbContext dbContext)
{
    public async Task<AspNetUser?> GetBoundUserAsync(string lineUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lineUserId))
        {
            return null;
        }

        return await dbContext.AspNetUsers
            .FirstOrDefaultAsync(x => x.LineMessagingApiUserId == lineUserId, cancellationToken);
    }

    public async Task<LineMyListingsSummary> GetMyListingsSummaryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var counts = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.SellerId == userId)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var active = counts.FirstOrDefault(x => x.Status == (int)ListingStatus.Active)?.Count ?? 0;
        var reserved = counts.FirstOrDefault(x => x.Status == (int)ListingStatus.Reserved)?.Count ?? 0;
        var sold = counts.FirstOrDefault(x => x.Status == (int)ListingStatus.Sold)?.Count ?? 0;
        var total = counts.Sum(x => x.Count);

        return new LineMyListingsSummary(total, active, reserved, sold);
    }

    public async Task<IReadOnlyList<LineMyListingCardItem>> GetMyListingCardItemsAsync(
        string userId,
        int maxItems = 5,
        CancellationToken cancellationToken = default)
    {
        maxItems = Math.Clamp(maxItems, 1, 5);

        var listings = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.SellerId == userId)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Status,
                x.IsFree,
                x.Price,
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync(cancellationToken);
        if (listings.Count == 0)
        {
            return Array.Empty<LineMyListingCardItem>();
        }

        var listingIds = listings.Select(x => x.Id).ToList();

        var favoriteCounts = await dbContext.ListingFavorites
            .AsNoTracking()
            .Where(x => listingIds.Contains(x.ListingId))
            .GroupBy(x => x.ListingId)
            .Select(g => new { ListingId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ListingId, x => x.Count, cancellationToken);

        var unreadConversationListingIds = await (
            from c in dbContext.Conversations.AsNoTracking()
            join m in dbContext.Messages.AsNoTracking() on c.Id equals m.ConversationId
            where listingIds.Contains(c.ListingId)
                && (c.Participant1Id == userId || c.Participant2Id == userId)
                && m.SenderId != userId
                && ((c.Participant1Id == userId && (c.Participant1LastReadAt == null || m.CreatedAt > c.Participant1LastReadAt.Value))
                    || (c.Participant2Id == userId && (c.Participant2LastReadAt == null || m.CreatedAt > c.Participant2LastReadAt.Value)))
            select c.ListingId
        )
        .Distinct()
        .ToListAsync(cancellationToken);

        var unreadListingIdSet = unreadConversationListingIds.ToHashSet();

        return listings
            .Select(x =>
            {
                favoriteCounts.TryGetValue(x.Id, out var favoriteCount);
                return new LineMyListingCardItem(
                    x.Id,
                    x.Title,
                    x.Status,
                    x.IsFree,
                    x.Price,
                    favoriteCount,
                    LastStatusChangedAt: x.UpdatedAt,
                    HasUnreadMessages: unreadListingIdSet.Contains(x.Id),
                    x.UpdatedAt,
                    x.CreatedAt);
            })
            .OrderByDescending(x => x.HasUnreadMessages)
            .ThenByDescending(x => x.LastStatusChangedAt)
            .ThenByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Take(maxItems)
            .ToList();
    }

    public async Task<LineMyMessagesSummary> GetMyMessagesSummaryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var conversations = await dbContext.Conversations
            .AsNoTracking()
            .Where(c => c.Participant1Id == userId || c.Participant2Id == userId)
            .Select(c => new
            {
                c.Id,
                c.Participant1Id,
                c.Participant1LastReadAt,
                c.Participant2LastReadAt,
                c.UpdatedAt,
                Participant1DisplayName = c.Participant1.DisplayName,
                Participant2DisplayName = c.Participant2.DisplayName
            })
            .ToListAsync(cancellationToken);

        if (conversations.Count == 0)
        {
            return new LineMyMessagesSummary(0, 0, Array.Empty<LineUnreadConversationQuickLink>());
        }

        var unreadCounts1 = await (
            from c in dbContext.Conversations.AsNoTracking()
            where c.Participant1Id == userId
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
            where c.Participant2Id == userId
            from m in dbContext.Messages.AsNoTracking()
            where m.ConversationId == c.Id
                && m.SenderId != userId
                && (c.Participant2LastReadAt == null || m.CreatedAt > c.Participant2LastReadAt.Value)
            group m by c.Id
            into g
            select new { ConversationId = g.Key, UnreadCount = g.Count() }
        ).ToDictionaryAsync(x => x.ConversationId, x => x.UnreadCount, cancellationToken);

        var unreadCountByConversationId = new Dictionary<Guid, int>(unreadCounts1);
        foreach (var kv in unreadCounts2)
        {
            unreadCountByConversationId[kv.Key] = kv.Value;
        }

        var unreadConversationIds = unreadCountByConversationId
            .Where(x => x.Value > 0)
            .Select(x => x.Key)
            .ToList();

        var latestMessageAtByConversationId = unreadConversationIds.Count == 0
            ? new Dictionary<Guid, DateTime>()
            : await dbContext.Messages
                .AsNoTracking()
                .Where(m => unreadConversationIds.Contains(m.ConversationId))
                .GroupBy(m => m.ConversationId)
                .Select(g => new { ConversationId = g.Key, LatestAt = g.Max(x => x.CreatedAt) })
                .ToDictionaryAsync(x => x.ConversationId, x => x.LatestAt, cancellationToken);

        var unreadQuickLinks = conversations
            .Select(c =>
            {
                unreadCountByConversationId.TryGetValue(c.Id, out var unreadCount);
                if (unreadCount <= 0)
                {
                    return null;
                }

                var otherDisplayName = c.Participant1Id == userId
                    ? c.Participant2DisplayName ?? "對方"
                    : c.Participant1DisplayName ?? "對方";

                latestMessageAtByConversationId.TryGetValue(c.Id, out var latestMessageAt);
                return new LineUnreadConversationQuickLink(
                    c.Id,
                    otherDisplayName,
                    unreadCount,
                    latestMessageAt == default ? c.UpdatedAt : latestMessageAt);
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => x.LatestMessageAt)
            .Take(3)
            .ToList();

        var unreadTotal = unreadCountByConversationId.Values.Sum();
        return new LineMyMessagesSummary(conversations.Count, unreadTotal, unreadQuickLinks);
    }
}

public sealed record LineMyListingsSummary(
    int Total,
    int Active,
    int Reserved,
    int Sold);

public sealed record LineMyMessagesSummary(
    int ConversationCount,
    int UnreadCount,
    IReadOnlyList<LineUnreadConversationQuickLink> UnreadQuickLinks);

public sealed record LineMyListingCardItem(
    Guid ListingId,
    string Title,
    int Status,
    bool IsFree,
    decimal Price,
    int FavoriteCount,
    DateTime LastStatusChangedAt,
    bool HasUnreadMessages,
    DateTime UpdatedAt,
    DateTime CreatedAt);

public sealed record LineUnreadConversationQuickLink(
    Guid ConversationId,
    string OtherDisplayName,
    int UnreadCount,
    DateTime LatestMessageAt);
