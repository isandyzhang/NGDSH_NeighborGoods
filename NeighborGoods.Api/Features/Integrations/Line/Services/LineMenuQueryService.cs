using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Listing;
using NeighborGoods.Api.Infrastructure.Storage;
using NeighborGoods.Data;
using NeighborGoods.Data.LegacyEntities;
using NeighborGoods.Notifications;

namespace NeighborGoods.Api.Features.Integrations.Line.Services;

public sealed class LineMenuQueryService(
    NeighborGoodsDbContext dbContext,
    IBlobStorage blobStorage)
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
        var activeStatus = (int)ListingStatus.Active;
        var reservedStatus = (int)ListingStatus.Reserved;
        var residenceNameMap = await dbContext.ListingResidences
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Id, x.DisplayName })
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        var pickupLocationNameMap = await dbContext.ListingPickupLocations
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Id, x.DisplayName })
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        var listings = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.SellerId == userId && (x.Status == activeStatus || x.Status == reservedStatus))
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Description,
                x.Status,
                x.Residence,
                x.PickupLocation,
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
        var coverImageMap = await GetCoverImageMapAsync(listingIds, cancellationToken);

        var favoriteCounts = await dbContext.ListingFavorites
            .AsNoTracking()
            .Where(x => listingIds.Contains(x.ListingId))
            .GroupBy(x => x.ListingId)
            .Select(g => new { ListingId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ListingId, x => x.Count, cancellationToken);

        var unreadCountByListingId = await (
            from c in dbContext.Conversations.AsNoTracking()
            join m in dbContext.Messages.AsNoTracking() on c.Id equals m.ConversationId
            where listingIds.Contains(c.ListingId)
                && (c.Participant1Id == userId || c.Participant2Id == userId)
                && m.SenderId != userId
                && ((c.Participant1Id == userId && (c.Participant1LastReadAt == null || m.CreatedAt > c.Participant1LastReadAt.Value))
                    || (c.Participant2Id == userId && (c.Participant2LastReadAt == null || m.CreatedAt > c.Participant2LastReadAt.Value)))
            group m by c.ListingId
            into g
            select new { ListingId = g.Key, UnreadCount = g.Count() }
        )
        .ToDictionaryAsync(x => x.ListingId, x => x.UnreadCount, cancellationToken);

        return listings
            .Select(x =>
            {
                favoriteCounts.TryGetValue(x.Id, out var favoriteCount);
                coverImageMap.TryGetValue(x.Id, out var imageUrl);
                unreadCountByListingId.TryGetValue(x.Id, out var unreadCount);
                return new LineMyListingCardItem(
                    x.Id,
                    x.Title,
                    x.Description,
                    imageUrl,
                    x.Status,
                    residenceNameMap.GetValueOrDefault(x.Residence, "未指定"),
                    pickupLocationNameMap.GetValueOrDefault(x.PickupLocation, "私訊"),
                    x.IsFree,
                    x.Price,
                    favoriteCount,
                    LastStatusChangedAt: x.UpdatedAt,
                    UnreadCount: unreadCount,
                    x.UpdatedAt,
                    x.CreatedAt);
            })
            .OrderByDescending(x => x.Status == (int)ListingStatus.Reserved)
            .ThenByDescending(x => x.UnreadCount > 0)
            .ThenByDescending(x => x.LastStatusChangedAt)
            .ThenByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Take(maxItems)
            .ToList();
    }

    private async Task<Dictionary<Guid, string?>> GetCoverImageMapAsync(
        IReadOnlyCollection<Guid> listingIds,
        CancellationToken cancellationToken)
    {
        if (listingIds.Count == 0)
        {
            return [];
        }

        var images = await dbContext.ListingImages
            .AsNoTracking()
            .Where(x => listingIds.Contains(x.ListingId))
            .OrderBy(x => x.ListingId)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .Select(x => new { x.ListingId, x.ImageUrl })
            .ToListAsync(cancellationToken);

        return images
            .GroupBy(x => x.ListingId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var raw = g.FirstOrDefault()?.ImageUrl;
                    return string.IsNullOrWhiteSpace(raw) ? null : ResolveImageUrl(raw);
                });
    }

    private string ResolveImageUrl(string storedPathOrUrl)
    {
        if (storedPathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            storedPathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return storedPathOrUrl;
        }

        return blobStorage.BuildPublicUrl(storedPathOrUrl);
    }

    public async Task<LineMyMessagesSummary> GetMyMessagesSummaryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var selfUser = await dbContext.AspNetUsers
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new { x.Id, x.DisplayName, x.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken);

        var conversations = await dbContext.Conversations
            .AsNoTracking()
            .Where(c => c.Participant1Id == userId || c.Participant2Id == userId)
            .Select(c => new
            {
                c.Id,
                c.ListingId,
                c.Participant1Id,
                c.Participant1LastReadAt,
                c.Participant2LastReadAt,
                c.UpdatedAt,
                ListingTitle = c.Listing.Title,
                Participant1DisplayName = c.Participant1.DisplayName,
                Participant2DisplayName = c.Participant2.DisplayName
            })
            .ToListAsync(cancellationToken);

        if (conversations.Count == 0)
        {
            return new LineMyMessagesSummary(
                0,
                0,
                selfUser?.DisplayName ?? selfUser?.Id ?? userId,
                selfUser?.CreatedAt,
                Array.Empty<LineRecentConversationItem>());
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

        var conversationIds = conversations.Select(x => x.Id).ToList();
        var latestMessageByConversationId = conversationIds.Count == 0
            ? new Dictionary<Guid, (string Content, DateTime CreatedAt)>()
            : (await dbContext.Messages
                .AsNoTracking()
                .Where(m => conversationIds.Contains(m.ConversationId))
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new
                {
                    m.ConversationId,
                    m.Content,
                    m.CreatedAt
                })
                .ToListAsync(cancellationToken))
                .GroupBy(m => m.ConversationId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var latest = g.First();
                        return (latest.Content, latest.CreatedAt);
                    });

        var recentConversations = conversations
            .Select(c =>
            {
                unreadCountByConversationId.TryGetValue(c.Id, out var unreadCount);
                var otherDisplayName = c.Participant1Id == userId
                    ? c.Participant2DisplayName ?? "對方"
                    : c.Participant1DisplayName ?? "對方";
                latestMessageByConversationId.TryGetValue(c.Id, out var latestMessage);
                var latestMessageContent = string.IsNullOrWhiteSpace(latestMessage.Content)
                    ? "（無訊息內容）"
                    : latestMessage.Content;
                var latestMessageAt = latestMessage.CreatedAt == default ? c.UpdatedAt : latestMessage.CreatedAt;

                return new LineRecentConversationItem(
                    c.Id,
                    otherDisplayName,
                    c.ListingTitle,
                    latestMessageContent,
                    latestMessageAt,
                    unreadCount);
            })
            .OrderByDescending(x => x.UnreadCount > 0)
            .ThenByDescending(x => x.LatestMessageAt)
            .Take(3)
            .ToList();

        var unreadTotal = unreadCountByConversationId.Values.Sum();
        return new LineMyMessagesSummary(
            conversations.Count,
            unreadTotal,
            selfUser?.DisplayName ?? selfUser?.Id ?? userId,
            selfUser?.CreatedAt,
            recentConversations);
    }
}
