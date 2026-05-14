using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeighborGoods.Data;
using NeighborGoods.Data.LegacyEntities;
using NeighborGoods.Data.PurchaseRequests;
using NeighborGoods.Messaging;
using NeighborGoods.Notifications;

namespace NeighborGoods.Workers.PurchaseRequests;

public sealed class PurchaseRequestScheduledOperations(
    NeighborGoodsDbContext dbContext,
    ILineMessageSender lineMessageSender,
    LinePushPolicyService linePushPolicyService,
    LineFlexMessageBuilder lineFlexMessageBuilder,
    ISystemMessageRealtimePublisher systemMessageRealtimePublisher,
    ILogger<PurchaseRequestScheduledOperations> logger) : IPurchaseRequestScheduledOperations
{
    private const string ExpireRequestSystemMessage = "[系統發送]此交易請求已逾時失效。";
    private const string ReminderRequestSystemMessage = "[系統發送]此交易請求剩餘 1 小時，請盡快回覆。";

    public async Task<int> ExpirePendingAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var pendingStatus = (int)PurchaseRequestStatus.Pending;
        var requests = await dbContext.PurchaseRequests
            .Where(x => x.Status == pendingStatus && x.ExpireAt <= now)
            .ToListAsync(cancellationToken);

        if (requests.Count == 0)
        {
            return 0;
        }

        var conversations = await LoadConversationsByIdsAsync(
            requests.Select(x => x.ConversationId),
            cancellationToken);

        foreach (var request in requests)
        {
            request.Status = (int)PurchaseRequestStatus.Expired;
            request.RespondedAt = now;
            request.ResponseReason = "此交易請求已逾時失效";
            await AddSystemMessageAsync(
                request.ConversationId,
                request.SellerId,
                ExpireRequestSystemMessage,
                now,
                conversations,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await systemMessageRealtimePublisher.PublishLatestSystemMessagesAsync(
            requests.Select(x => x.ConversationId),
            cancellationToken);
        return requests.Count;
    }

    public async Task<int> SendSellerReminderAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var remindUntil = now.Add(PurchaseRequestConstants.SellerReminderLeadTime);
        var pendingStatus = (int)PurchaseRequestStatus.Pending;

        var requests = await dbContext.PurchaseRequests
            .Where(x =>
                x.Status == pendingStatus
                && x.ExpireAt > now
                && x.ExpireAt <= remindUntil
                && x.SellerReminderSentAt == null)
            .ToListAsync(cancellationToken);

        if (requests.Count == 0)
        {
            return 0;
        }

        var conversations = await LoadConversationsByIdsAsync(
            requests.Select(x => x.ConversationId),
            cancellationToken);
        var sellerIds = requests.Select(x => x.SellerId).Distinct().ToList();
        var listingIds = requests.Select(x => x.ListingId).Distinct().ToList();
        var sellers = await dbContext.AspNetUsers
            .Where(x => sellerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var listings = await dbContext.Listings
            .AsNoTracking()
            .Where(x => listingIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Title })
            .ToDictionaryAsync(x => x.Id, x => x.Title, cancellationToken);

        foreach (var request in requests)
        {
            request.SellerReminderSentAt = now;
            await AddSystemMessageAsync(
                request.ConversationId,
                request.BuyerId,
                ReminderRequestSystemMessage,
                now,
                conversations,
                cancellationToken);

            if (!sellers.TryGetValue(request.SellerId, out var seller))
            {
                continue;
            }

            if (!linePushPolicyService.CanSendTransactionalPush(seller, now))
            {
                continue;
            }

            try
            {
                var listingTitle = listings.TryGetValue(request.ListingId, out var title)
                    ? title
                    : "未命名商品";
                var card = lineFlexMessageBuilder.BuildPurchaseRequestReminderCard(
                    listingTitle,
                    request.ConversationId);
                await lineMessageSender.PushFlexAsync(
                    seller.LineMessagingApiUserId!,
                    card.AltText,
                    card.Contents,
                    cancellationToken);
                seller.LineNotificationLastSentAt = now;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "LINE 發送交易提醒失敗：SellerId={SellerId}, RequestId={RequestId}",
                    request.SellerId,
                    request.Id);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return requests.Count;
    }

    private async Task AddSystemMessageAsync(
        Guid conversationId,
        string senderId,
        string content,
        DateTime now,
        IReadOnlyDictionary<Guid, Conversation>? preloadedConversations,
        CancellationToken cancellationToken)
    {
        dbContext.Messages.Add(new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content,
            CreatedAt = now
        });

        Conversation? conversation = null;
        if (preloadedConversations is not null)
        {
            preloadedConversations.TryGetValue(conversationId, out conversation);
        }
        else
        {
            conversation = await dbContext.Conversations
                .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken);
        }

        if (conversation is not null)
        {
            conversation.UpdatedAt = now;
        }
    }

    private async Task<Dictionary<Guid, Conversation>> LoadConversationsByIdsAsync(
        IEnumerable<Guid> conversationIds,
        CancellationToken cancellationToken)
    {
        var ids = conversationIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, Conversation>();
        }

        return await dbContext.Conversations
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }
}
