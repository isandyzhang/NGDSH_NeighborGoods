using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Integrations.Line.Services;
using NeighborGoods.Api.Features.Listing;
using NeighborGoods.Api.Features.PurchaseRequests.Contracts.Responses;
using NeighborGoods.Api.Shared.Notifications;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Features.PurchaseRequests.Services;

public sealed class PurchaseRequestService(
    NeighborGoodsDbContext dbContext,
    ILineMessageSender lineMessageSender,
    LinePushPolicyService linePushPolicyService,
    LineFlexMessageBuilder lineFlexMessageBuilder,
    ILogger<PurchaseRequestService> logger)
{
    private const string CreateRequestSystemMessage = "[系統發送]買家已送出購買請求，請於 12 小時內回覆。";
    private const string AcceptRequestSystemMessage = "[系統發送]賣家已接受此交易，商品已保留。";
    private const string RejectRequestSystemMessage = "[系統發送]賣家已婉拒此交易請求。";
    private const string CancelRequestSystemMessage = "[系統發送]買家已取消此交易請求。";
    private const string ExpireRequestSystemMessage = "[系統發送]此交易請求已逾時失效。";
    private const string ReminderRequestSystemMessage = "[系統發送]此交易請求剩餘 1 小時，請盡快回覆。";

    public async Task<(PurchaseRequestResponse? Data, string? ErrorCode, string? ErrorMessage)> CreateAsync(
        string currentUserId,
        Guid listingId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await ExpireOverdueForListingAsync(listingId, now, cancellationToken);

        var listing = await dbContext.Listings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == listingId, cancellationToken);

        if (listing is null)
        {
            return (null, "LISTING_NOT_FOUND", "找不到商品");
        }

        if (string.Equals(listing.SellerId, currentUserId, StringComparison.Ordinal))
        {
            return (null, "SELF_PURCHASE_NOT_ALLOWED", "賣家不能對自己的商品送出購買請求");
        }

        if ((ListingStatus)listing.Status != ListingStatus.Active)
        {
            return (null, "LISTING_NOT_AVAILABLE", "此商品目前不可發起購買請求");
        }

        var buyerExists = await dbContext.AspNetUsers
            .AsNoTracking()
            .AnyAsync(x => x.Id == currentUserId, cancellationToken);
        if (!buyerExists)
        {
            return (null, "USER_NOT_FOUND", "找不到目前使用者");
        }

        var participant1Id = string.CompareOrdinal(currentUserId, listing.SellerId) < 0
            ? currentUserId
            : listing.SellerId;
        var participant2Id = string.CompareOrdinal(currentUserId, listing.SellerId) < 0
            ? listing.SellerId
            : currentUserId;

        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(
                c => c.ListingId == listing.Id
                     && c.Participant1Id == participant1Id
                     && c.Participant2Id == participant2Id,
                cancellationToken);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                ListingId = listing.Id,
                Participant1Id = participant1Id,
                Participant2Id = participant2Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.Conversations.Add(conversation);
        }

        var pendingStatus = (int)PurchaseRequestStatus.Pending;
        var existingPending = await dbContext.PurchaseRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.ListingId == listing.Id && x.Status == pendingStatus,
                cancellationToken);
        if (existingPending is not null)
        {
            return (null, "PURCHASE_REQUEST_ALREADY_PENDING", "此商品已有待回覆的交易請求");
        }

        var request = new PurchaseRequest
        {
            Id = Guid.NewGuid(),
            ListingId = listing.Id,
            ConversationId = conversation.Id,
            BuyerId = currentUserId,
            SellerId = listing.SellerId,
            Status = pendingStatus,
            CreatedAt = now,
            ExpireAt = now.Add(PurchaseRequestConstants.SellerResponseWindow)
        };
        dbContext.PurchaseRequests.Add(request);

        await AddSystemMessageAsync(
            conversation.Id,
            request.BuyerId,
            CreateRequestSystemMessage,
            now,
            cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return (null, "PURCHASE_REQUEST_ALREADY_PENDING", "此商品已有待回覆的交易請求");
        }

        return (ToResponse(request, DateTime.UtcNow), null, null);
    }

    public async Task<(PurchaseRequestResponse? Data, string? ErrorCode, string? ErrorMessage)> AcceptAsync(
        string currentUserId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        return await RespondAsync(
            currentUserId,
            requestId,
            PurchaseRequestStatus.Accepted,
            responseReason: null,
            cancellationToken);
    }

    public async Task<(PurchaseRequestResponse? Data, string? ErrorCode, string? ErrorMessage)> RejectAsync(
        string currentUserId,
        Guid requestId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        return await RespondAsync(
            currentUserId,
            requestId,
            PurchaseRequestStatus.Rejected,
            reason,
            cancellationToken);
    }

    public async Task<(PurchaseRequestResponse? Data, string? ErrorCode, string? ErrorMessage)> CancelAsync(
        string currentUserId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var request = await dbContext.PurchaseRequests
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (request is null)
        {
            return (null, "PURCHASE_REQUEST_NOT_FOUND", "找不到交易請求");
        }

        if (!string.Equals(request.BuyerId, currentUserId, StringComparison.Ordinal))
        {
            return (null, "PURCHASE_REQUEST_ACCESS_DENIED", "僅買家本人可取消交易請求");
        }

        var now = DateTime.UtcNow;
        if (!TryEnsurePending(request, now, out var errorCode, out var errorMessage))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return (null, errorCode, errorMessage);
        }

        request.Status = (int)PurchaseRequestStatus.Cancelled;
        request.RespondedAt = now;
        request.ResponseReason = null;
        await AddSystemMessageAsync(
            request.ConversationId,
            request.BuyerId,
            CancelRequestSystemMessage,
            now,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (ToResponse(request, now), null, null);
    }

    public async Task<(PurchaseRequestResponse? Data, string? ErrorCode, string? ErrorMessage)> GetByIdAsync(
        string currentUserId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var request = await dbContext.PurchaseRequests
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (request is null)
        {
            return (null, "PURCHASE_REQUEST_NOT_FOUND", "找不到交易請求");
        }

        var isParticipant = string.Equals(request.BuyerId, currentUserId, StringComparison.Ordinal)
                            || string.Equals(request.SellerId, currentUserId, StringComparison.Ordinal);
        if (!isParticipant)
        {
            return (null, "PURCHASE_REQUEST_ACCESS_DENIED", "無權限查看此交易請求");
        }

        var now = DateTime.UtcNow;
        if ((PurchaseRequestStatus)request.Status == PurchaseRequestStatus.Pending
            && request.ExpireAt <= now)
        {
            request.Status = (int)PurchaseRequestStatus.Expired;
            request.RespondedAt = now;
            request.ResponseReason = "逾時未回覆";
            await AddSystemMessageAsync(
                request.ConversationId,
                request.SellerId,
                ExpireRequestSystemMessage,
                now,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return (ToResponse(request, DateTime.UtcNow), null, null);
    }

    public async Task<(PurchaseRequestResponse? Data, string? ErrorCode, string? ErrorMessage)> GetCurrentByConversationAsync(
        string currentUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var (conversation, conversationErrorCode, conversationErrorMessage) = await EnsureConversationParticipantAsync(
            currentUserId,
            conversationId,
            cancellationToken);
        if (conversation is null)
        {
            return (null, conversationErrorCode, conversationErrorMessage);
        }

        var request = await dbContext.PurchaseRequests
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.ConversationId == conversationId, cancellationToken);
        if (request is null)
        {
            return (null, "PURCHASE_REQUEST_NOT_FOUND", "找不到交易請求");
        }

        var now = DateTime.UtcNow;
        if ((PurchaseRequestStatus)request.Status == PurchaseRequestStatus.Pending
            && request.ExpireAt <= now)
        {
            request.Status = (int)PurchaseRequestStatus.Expired;
            request.RespondedAt = now;
            request.ResponseReason = "逾時未回覆";
            await AddSystemMessageAsync(
                request.ConversationId,
                request.SellerId,
                ExpireRequestSystemMessage,
                now,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return (ToResponse(request, DateTime.UtcNow), null, null);
    }

    public async Task<(PurchaseRequestResponse? Data, string? ErrorCode, string? ErrorMessage)> AcceptByConversationAsync(
        string currentUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var (requestId, errorCode, errorMessage) = await GetPendingRequestIdByConversationAsync(
            currentUserId,
            conversationId,
            cancellationToken);
        if (requestId is null)
        {
            return (null, errorCode, errorMessage);
        }

        return await AcceptAsync(currentUserId, requestId.Value, cancellationToken);
    }

    public async Task<(PurchaseRequestResponse? Data, string? ErrorCode, string? ErrorMessage)> RejectByConversationAsync(
        string currentUserId,
        Guid conversationId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var (requestId, errorCode, errorMessage) = await GetPendingRequestIdByConversationAsync(
            currentUserId,
            conversationId,
            cancellationToken);
        if (requestId is null)
        {
            return (null, errorCode, errorMessage);
        }

        return await RejectAsync(currentUserId, requestId.Value, reason, cancellationToken);
    }

    public async Task<(PurchaseRequestResponse? Data, string? ErrorCode, string? ErrorMessage)> CancelByConversationAsync(
        string currentUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var (requestId, errorCode, errorMessage) = await GetPendingRequestIdByConversationAsync(
            currentUserId,
            conversationId,
            cancellationToken);
        if (requestId is null)
        {
            return (null, errorCode, errorMessage);
        }

        return await CancelAsync(currentUserId, requestId.Value, cancellationToken);
    }

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

        foreach (var request in requests)
        {
            request.Status = (int)PurchaseRequestStatus.Expired;
            request.RespondedAt = now;
            request.ResponseReason = "逾時未回覆";
            await AddSystemMessageAsync(
                request.ConversationId,
                request.SellerId,
                ExpireRequestSystemMessage,
                now,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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

        var sellerIds = requests.Select(x => x.SellerId).Distinct().ToList();
        var sellers = await dbContext.AspNetUsers
            .Where(x => sellerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var request in requests)
        {
            request.SellerReminderSentAt = now;
            await AddSystemMessageAsync(
                request.ConversationId,
                request.BuyerId,
                ReminderRequestSystemMessage,
                now,
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
                var card = lineFlexMessageBuilder.BuildNoticeCard(
                    "交易請求提醒",
                    "有一筆購買請求即將逾時，請盡快回覆。");
                await lineMessageSender.PushFlexAsync(seller.LineMessagingApiUserId!, card.AltText, card.Contents, cancellationToken);
                seller.LineNotificationLastSentAt = now;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "發送 LINE 交易提醒失敗：SellerId={SellerId}, RequestId={RequestId}", request.SellerId, request.Id);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return requests.Count;
    }

    private async Task<(PurchaseRequestResponse? Data, string? ErrorCode, string? ErrorMessage)> RespondAsync(
        string currentUserId,
        Guid requestId,
        PurchaseRequestStatus nextStatus,
        string? responseReason,
        CancellationToken cancellationToken)
    {
        var request = await dbContext.PurchaseRequests
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (request is null)
        {
            return (null, "PURCHASE_REQUEST_NOT_FOUND", "找不到交易請求");
        }

        if (!string.Equals(request.SellerId, currentUserId, StringComparison.Ordinal))
        {
            return (null, "PURCHASE_REQUEST_ACCESS_DENIED", "僅賣家本人可回覆交易請求");
        }

        var now = DateTime.UtcNow;
        if (!TryEnsurePending(request, now, out var errorCode, out var errorMessage))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return (null, errorCode, errorMessage);
        }

        if (nextStatus == PurchaseRequestStatus.Accepted)
        {
            var listing = await dbContext.Listings
                .FirstOrDefaultAsync(x => x.Id == request.ListingId, cancellationToken);
            if (listing is null)
            {
                return (null, "LISTING_NOT_FOUND", "找不到商品");
            }

            if (!string.Equals(listing.SellerId, request.SellerId, StringComparison.Ordinal))
            {
                return (null, "PURCHASE_REQUEST_SELLER_MISMATCH", "交易請求與商品賣家不一致");
            }

            var currentStatus = (ListingStatus)listing.Status;
            if (currentStatus != ListingStatus.Reserved
                && !ListingStatusRules.CanTransition(currentStatus, ListingStatus.Reserved))
            {
                return (null, "LISTING_INVALID_STATUS_TRANSITION", "目前商品狀態不可接受交易請求");
            }

            listing.Status = (int)ListingStatus.Reserved;
            listing.UpdatedAt = now;
        }

        request.Status = (int)nextStatus;
        request.RespondedAt = now;
        request.ResponseReason = string.IsNullOrWhiteSpace(responseReason) ? null : responseReason.Trim();

        var message = nextStatus switch
        {
            PurchaseRequestStatus.Accepted => AcceptRequestSystemMessage,
            PurchaseRequestStatus.Rejected => RejectRequestSystemMessage,
            _ => null
        };
        if (!string.IsNullOrEmpty(message))
        {
            await AddSystemMessageAsync(
                request.ConversationId,
                request.SellerId,
                message,
                now,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (ToResponse(request, now), null, null);
    }

    private async Task ExpireOverdueForListingAsync(
        Guid listingId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var pendingStatus = (int)PurchaseRequestStatus.Pending;
        var expired = await dbContext.PurchaseRequests
            .Where(x => x.ListingId == listingId && x.Status == pendingStatus && x.ExpireAt <= now)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return;
        }

        foreach (var request in expired)
        {
            request.Status = (int)PurchaseRequestStatus.Expired;
            request.RespondedAt = now;
            request.ResponseReason = "逾時未回覆";
            await AddSystemMessageAsync(
                request.ConversationId,
                request.SellerId,
                ExpireRequestSystemMessage,
                now,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<(Guid? RequestId, string? ErrorCode, string? ErrorMessage)> GetPendingRequestIdByConversationAsync(
        string currentUserId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var (conversation, conversationErrorCode, conversationErrorMessage) = await EnsureConversationParticipantAsync(
            currentUserId,
            conversationId,
            cancellationToken);
        if (conversation is null)
        {
            return (null, conversationErrorCode, conversationErrorMessage);
        }

        var now = DateTime.UtcNow;
        var pendingStatus = (int)PurchaseRequestStatus.Pending;
        var pendingRequest = await dbContext.PurchaseRequests
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(
                x => x.ConversationId == conversationId
                     && x.Status == pendingStatus,
                cancellationToken);
        if (pendingRequest is null)
        {
            return (null, "PURCHASE_REQUEST_NOT_FOUND", "找不到待回覆交易請求");
        }

        if (pendingRequest.ExpireAt <= now)
        {
            pendingRequest.Status = (int)PurchaseRequestStatus.Expired;
            pendingRequest.RespondedAt = now;
            pendingRequest.ResponseReason = "逾時未回覆";
            await AddSystemMessageAsync(
                pendingRequest.ConversationId,
                pendingRequest.SellerId,
                ExpireRequestSystemMessage,
                now,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (null, "PURCHASE_REQUEST_EXPIRED", "此交易請求已逾時");
        }

        return (pendingRequest.Id, null, null);
    }

    private async Task<(Conversation? Conversation, string? ErrorCode, string? ErrorMessage)> EnsureConversationParticipantAsync(
        string currentUserId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken);
        if (conversation is null)
        {
            return (null, "CONVERSATION_NOT_FOUND", "找不到對話");
        }

        if (!string.Equals(conversation.Participant1Id, currentUserId, StringComparison.Ordinal)
            && !string.Equals(conversation.Participant2Id, currentUserId, StringComparison.Ordinal))
        {
            return (null, "CONVERSATION_ACCESS_DENIED", "無權限訪問此對話");
        }

        return (conversation, null, null);
    }

    private bool TryEnsurePending(
        PurchaseRequest request,
        DateTime now,
        out string? errorCode,
        out string? errorMessage)
    {
        if ((PurchaseRequestStatus)request.Status != PurchaseRequestStatus.Pending)
        {
            errorCode = "PURCHASE_REQUEST_NOT_PENDING";
            errorMessage = "此交易請求已完成或失效";
            return false;
        }

        if (request.ExpireAt <= now)
        {
            request.Status = (int)PurchaseRequestStatus.Expired;
            request.RespondedAt = now;
            request.ResponseReason = "逾時未回覆";
            errorCode = "PURCHASE_REQUEST_EXPIRED";
            errorMessage = "此交易請求已逾時";
            return false;
        }

        errorCode = null;
        errorMessage = null;
        return true;
    }

    private async Task AddSystemMessageAsync(
        Guid conversationId,
        string senderId,
        string content,
        DateTime now,
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

        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken);
        if (conversation is not null)
        {
            conversation.UpdatedAt = now;
        }
    }

    private static PurchaseRequestResponse ToResponse(PurchaseRequest request, DateTime now)
    {
        var remaining = request.ExpireAt <= now
            ? 0
            : (int)Math.Ceiling((request.ExpireAt - now).TotalSeconds);

        return new PurchaseRequestResponse
        {
            Id = request.Id,
            ListingId = request.ListingId,
            ConversationId = request.ConversationId,
            BuyerId = request.BuyerId,
            SellerId = request.SellerId,
            Status = (PurchaseRequestStatus)request.Status,
            CreatedAt = request.CreatedAt,
            ExpireAt = request.ExpireAt,
            RespondedAt = request.RespondedAt,
            ResponseReason = request.ResponseReason,
            RemainingSeconds = Math.Max(remaining, 0)
        };
    }
}
