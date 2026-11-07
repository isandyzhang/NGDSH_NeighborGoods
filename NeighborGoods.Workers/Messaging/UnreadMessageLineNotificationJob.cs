using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeighborGoods.Data;
using NeighborGoods.Notifications;
using NeighborGoods.Workers.Listings;

namespace NeighborGoods.Workers.Messaging;

public sealed class UnreadMessageLineNotificationJob(
    NeighborGoodsDbContext dbContext,
    ILineMessageSender lineMessageSender,
    LineFlexMessageBuilder flexMessageBuilder,
    ILogger<UnreadMessageLineNotificationJob> logger)
{
    private const int UnreadDelayMinutes = 5;

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var thresholdTime = now.AddMinutes(-UnreadDelayMinutes);

        var users = await dbContext.AspNetUsers
            .Where(x =>
                x.LineMessagingApiUserId != null &&
                x.LineMessagingApiUserId != string.Empty &&
                x.LineMessagingApiAuthorizedAt != null)
            .Select(x => new
            {
                x.Id,
                x.LineMessagingApiUserId,
                x.LineUnreadNotificationLastSentAt
            })
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            return;
        }

        var userIds = users.Select(x => x.Id).ToHashSet();
        var lineUserIdDict = users.ToDictionary(x => x.Id, x => x.LineMessagingApiUserId!);
        var lineUnreadLastSentAtDict = users.ToDictionary(x => x.Id, x => x.LineUnreadNotificationLastSentAt);

        var conversations = await dbContext.Conversations
            .Where(x => userIds.Contains(x.Participant1Id) || userIds.Contains(x.Participant2Id))
            .Select(x => new
            {
                x.Id,
                x.ListingId,
                x.Participant1Id,
                x.Participant2Id,
                x.Participant1LastReadAt,
                x.Participant2LastReadAt
            })
            .ToListAsync(cancellationToken);

        if (conversations.Count == 0)
        {
            return;
        }

        var conversationDict = conversations.ToDictionary(x => x.Id);
        var conversationIds = conversations.Select(x => x.Id).ToList();

        var candidateMessages = await dbContext.Messages
            .Where(x => conversationIds.Contains(x.ConversationId) && x.CreatedAt <= thresholdTime)
            .Select(x => new
            {
                x.ConversationId,
                x.SenderId,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        if (candidateMessages.Count == 0)
        {
            return;
        }

        var usersToNotify = new Dictionary<string, Guid>();
        foreach (var message in candidateMessages)
        {
            if (!conversationDict.TryGetValue(message.ConversationId, out var conversation))
            {
                continue;
            }

            string? targetUserId = null;
            DateTime? lastReadAt = null;

            if (conversation.Participant1Id != message.SenderId)
            {
                targetUserId = conversation.Participant1Id;
                lastReadAt = conversation.Participant1LastReadAt;
            }
            else if (conversation.Participant2Id != message.SenderId)
            {
                targetUserId = conversation.Participant2Id;
                lastReadAt = conversation.Participant2LastReadAt;
            }

            if (string.IsNullOrWhiteSpace(targetUserId))
            {
                continue;
            }

            if (lastReadAt.HasValue && message.CreatedAt <= lastReadAt.Value)
            {
                continue;
            }

            if (!lineUnreadLastSentAtDict.TryGetValue(targetUserId, out var lineLastSentAt) ||
                (lineLastSentAt.HasValue && message.CreatedAt <= lineLastSentAt.Value))
            {
                continue;
            }

            if (!usersToNotify.ContainsKey(targetUserId))
            {
                usersToNotify[targetUserId] = message.ConversationId;
            }
        }

        if (usersToNotify.Count == 0)
        {
            return;
        }

        var notifyConversationIds = usersToNotify.Values.Distinct().ToList();
        var listingIds = conversations
            .Where(x => notifyConversationIds.Contains(x.Id))
            .Select(x => x.ListingId)
            .Distinct()
            .ToList();

        var listingTitles = await dbContext.Listings
            .AsNoTracking()
            .Where(x => listingIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Title })
            .ToDictionaryAsync(x => x.Id, x => x.Title, cancellationToken);

        var coverImages = await GetCoverImageMapAsync(listingIds, cancellationToken);

        var successCount = 0;
        foreach (var (userId, conversationId) in usersToNotify)
        {
            if (!lineUserIdDict.TryGetValue(userId, out var lineUserId))
            {
                continue;
            }

            if (!conversationDict.TryGetValue(conversationId, out var conversation))
            {
                continue;
            }

            try
            {
                var listingTitle = listingTitles.TryGetValue(conversation.ListingId, out var title)
                    ? title
                    : "未命名商品";
                var imageUrl = ListingImageUrlHelper.Resolve(
                    coverImages.GetValueOrDefault(conversation.ListingId));

                var card = flexMessageBuilder.BuildUnreadMessageCard(
                    new LineUnreadMessageItem(conversationId, listingTitle, imageUrl));

                await lineMessageSender.PushFlexAsync(
                    lineUserId,
                    card.AltText,
                    card.Contents,
                    cancellationToken);

                var user = await dbContext.AspNetUsers.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
                if (user != null)
                {
                    user.LineUnreadNotificationLastSentAt = now;
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to send unread message LINE notification. UserId={UserId}",
                    userId);
            }
        }

        if (successCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Dictionary<Guid, string?>> GetCoverImageMapAsync(
        IReadOnlyCollection<Guid> listingIds,
        CancellationToken cancellationToken)
    {
        if (listingIds.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.ListingImages
            .AsNoTracking()
            .Where(x => listingIds.Contains(x.ListingId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .Select(x => new { x.ListingId, x.ImageUrl })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.ListingId)
            .ToDictionary(g => g.Key, g => (string?)g.First().ImageUrl);
    }
}
