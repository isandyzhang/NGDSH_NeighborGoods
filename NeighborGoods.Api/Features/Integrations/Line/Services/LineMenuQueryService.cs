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
                c.Participant2LastReadAt
            })
            .ToListAsync(cancellationToken);

        if (conversations.Count == 0)
        {
            return new LineMyMessagesSummary(0, 0);
        }

        var unreadTotal = 0;
        foreach (var conversation in conversations)
        {
            var lastReadAt = conversation.Participant1Id == userId
                ? conversation.Participant1LastReadAt
                : conversation.Participant2LastReadAt;

            var unread = await dbContext.Messages
                .AsNoTracking()
                .CountAsync(
                    m => m.ConversationId == conversation.Id
                         && m.SenderId != userId
                         && (lastReadAt == null || m.CreatedAt > lastReadAt.Value),
                    cancellationToken);

            unreadTotal += unread;
        }

        return new LineMyMessagesSummary(conversations.Count, unreadTotal);
    }
}

public sealed record LineMyListingsSummary(
    int Total,
    int Active,
    int Reserved,
    int Sold);

public sealed record LineMyMessagesSummary(
    int ConversationCount,
    int UnreadCount);
