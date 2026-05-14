using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Integrations.Line.Services;
using NeighborGoods.Api.Features.Listing;
using NeighborGoods.Api.Features.Messaging.Contracts.Responses;
using NeighborGoods.Api.Features.PurchaseRequests.Contracts.Responses;
using NeighborGoods.Messaging;
using NeighborGoods.Workers.PurchaseRequests;

namespace NeighborGoods.Api.Features.PurchaseRequests.Services;

public sealed class PurchaseRequestService(
    NeighborGoodsDbContext dbContext,
    IPurchaseRequestScheduledOperations purchaseRequestScheduledOperations,
    ISystemMessageRealtimePublisher systemMessageRealtimePublisher)
{
    private const string CreateRequestSystemMessage = "[????]???????????? 12 ??????";
    private const string AcceptRequestSystemMessage = "[????]???????????????";
    private const string RejectRequestSystemMessage = "[????]???????????";
    private const string CancelRequestSystemMessage = "[????]???????????";
    private const string ExpireRequestSystemMessage = "[????]???????????";
    private const string ReminderRequestSystemMessage = "[????]??????? 1 ?????????";
    private const string SellerMarkedCompletedSystemMessage = "[????]???????????????????????";
    private const string BuyerConfirmedReceivedSystemMessage = "[????]????????????????";

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
            return (null, "LISTING_NOT_FOUND", "?????");
        }

        if (string.Equals(listing.SellerId, currentUserId, StringComparison.Ordinal))
        {
            return (null, "SELF_PURCHASE_NOT_ALLOWED", "????????????????");
        }

        if ((ListingStatus)listing.Status != ListingStatus.Active)
        {
            return (null, "LISTING_NOT_AVAILABLE", "?????????????");
        }

        var buyerExists = await dbContext.AspNetUsers
            .AsNoTracking()
            .AnyAsync(x => x.Id == currentUserId, cancellationToken);
        if (!buyerExists)
        {
            return (null, "USER_NOT_FOUND", "?????");
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
            return (null, "PURCHASE_REQUEST_ALREADY_PENDING", "????????????????");
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
            null,
            cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await systemMessageRealtimePublisher.PublishLatestSystemMessageAsync(conversation.Id, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return (null, "PURCHASE_REQUEST_ALREADY_PENDING", "????????????????");
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
            return (null, "PURCHASE_REQUEST_NOT_FOUND", "???????");
        }

        if (!string.Equals(request.BuyerId, currentUserId, StringComparison.Ordinal))
        {
            return (null, "PURCHASE_REQUEST_ACCESS_DENIED", "??????????");
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
            null,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await systemMessageRealtimePublisher.PublishLatestSystemMessageAsync(request.ConversationId, cancellationToken);

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
            return (null, "PURCHASE_REQUEST_NOT_FOUND", "???????");
        }

        var isParticipant = string.Equals(request.BuyerId, currentUserId, StringComparison.Ordinal)
                            || string.Equals(request.SellerId, currentUserId, StringComparison.Ordinal);
        if (!isParticipant)
        {
            return (null, "PURCHASE_REQUEST_ACCESS_DENIED", "??????????");
        }

        var now = DateTime.UtcNow;
        if ((PurchaseRequestStatus)request.Status == PurchaseRequestStatus.Pending
            && request.ExpireAt <= now)
        {
            request.Status = (int)PurchaseRequestStatus.Expired;
            request.RespondedAt = now;
            request.ResponseReason = "??????????";
            await AddSystemMessageAsync(
                request.ConversationId,
                request.SellerId,
                ExpireRequestSystemMessage,
                now,
                null,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await systemMessageRealtimePublisher.PublishLatestSystemMessageAsync(request.ConversationId, cancellationToken);
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
            return (null, "PURCHASE_REQUEST_NOT_FOUND", "???????");
        }

        var now = DateTime.UtcNow;
        if ((PurchaseRequestStatus)request.Status == PurchaseRequestStatus.Pending
            && request.ExpireAt <= now)
        {
            request.Status = (int)PurchaseRequestStatus.Expired;
            request.RespondedAt = now;
            request.ResponseReason = "??????????";
            await AddSystemMessageAsync(
                request.ConversationId,
                request.SellerId,
                ExpireRequestSystemMessage,
                now,
                null,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await systemMessageRealtimePublisher.PublishLatestSystemMessageAsync(request.ConversationId, cancellationToken);
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

    public async Task<(PurchaseRequestResponse? Data, string? ErrorCode, string? ErrorMessage)> CompleteBySellerByConversationAsync(
        string currentUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var (request, errorCode, errorMessage) = await GetCurrentRequestByConversationAsync(
            currentUserId,
            conversationId,
            cancellationToken);
        if (request is null)
        {
            return (null, errorCode, errorMessage);
        }

        return await MarkCompletedBySellerAsync(currentUserId, request.Id, cancellationToken);
    }

    public async Task<(PurchaseRequestResponse? Data, string? ErrorCode, string? ErrorMessage)> ConfirmReceivedByBuyerByConversationAsync(
        string currentUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var (request, errorCode, errorMessage) = await GetCurrentRequestByConversationAsync(
            currentUserId,
            conversationId,
            cancellationToken);
        if (request is null)
        {
            return (null, errorCode, errorMessage);
        }

        return await ConfirmReceivedByBuyerAsync(currentUserId, request.Id, cancellationToken);
    }

    public Task<int> ExpirePendingAsync(CancellationToken cancellationToken = default) =>
        purchaseRequestScheduledOperations.ExpirePendingAsync(cancellationToken);

    public Task<int> SendSellerReminderAsync(CancellationToken cancellationToken = default) =>
        purchaseRequestScheduledOperations.SendSellerReminderAsync(cancellationToken);

    public async Task<(PurchaseRequestResponse? Data, string? ErrorCode, string? ErrorMessage)> MarkCompletedBySellerAsync(
        string currentUserId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var request = await dbContext.PurchaseRequests
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (request is null)
        {
            return (null, "PURCHASE_REQUEST_NOT_FOUND", "???????");
        }

        if (!string.Equals(request.SellerId, currentUserId, StringComparison.Ordinal))
        {
            return (null, "PURCHASE_REQUEST_ACCESS_DENIED", "??????????");
        }

        if ((PurchaseRequestStatus)request.Status != PurchaseRequestStatus.Accepted)
        {
            return (null, "PURCHASE_REQUEST_INVALID_STATE", "?????????");
        }

        var listing = await dbContext.Listings
            .FirstOrDefaultAsync(x => x.Id == request.ListingId, cancellationToken);
        if (listing is null)
        {
            return (null, "LISTING_NOT_FOUND", "?????");
        }

        if (!string.Equals(listing.SellerId, request.SellerId, StringComparison.Ordinal))
        {
            return (null, "PURCHASE_REQUEST_SELLER_MISMATCH", "???????????????");
        }

        var now = DateTime.UtcNow;
        var targetStatus = ResolveCompletedListingStatus(listing);
        if (!ListingStatusRules.CanTransition((ListingStatus)listing.Status, targetStatus))
        {
            return (null, "LISTING_INVALID_STATUS_TRANSITION", "????????");
        }

        listing.Status = (int)targetStatus;
        listing.UpdatedAt = now;
        request.Status = (int)PurchaseRequestStatus.SellerMarkedCompleted;
        request.RespondedAt = now;
        request.ResponseReason = null;
        await AddSystemMessageAsync(
            request.ConversationId,
            request.SellerId,
            SellerMarkedCompletedSystemMessage,
            now,
            null,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await systemMessageRealtimePublisher.PublishLatestSystemMessageAsync(request.ConversationId, cancellationToken);
        return (ToResponse(request, now), null, null);
    }

    public async Task<(PurchaseRequestResponse? Data, string? ErrorCode, string? ErrorMessage)> ConfirmReceivedByBuyerAsync(
        string currentUserId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var request = await dbContext.PurchaseRequests
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (request is null)
        {
            return (null, "PURCHASE_REQUEST_NOT_FOUND", "???????");
        }

        if (!string.Equals(request.BuyerId, currentUserId, StringComparison.Ordinal))
        {
            return (null, "PURCHASE_REQUEST_ACCESS_DENIED", "??????????");
        }

        if ((PurchaseRequestStatus)request.Status != PurchaseRequestStatus.SellerMarkedCompleted)
        {
            return (null, "PURCHASE_REQUEST_INVALID_STATE", "?????????");
        }

        var now = DateTime.UtcNow;
        request.Status = (int)PurchaseRequestStatus.Completed;
        request.RespondedAt = now;
        request.ResponseReason = null;
        await AddSystemMessageAsync(
            request.ConversationId,
            request.BuyerId,
            BuyerConfirmedReceivedSystemMessage,
            now,
            null,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await systemMessageRealtimePublisher.PublishLatestSystemMessageAsync(request.ConversationId, cancellationToken);
        return (ToResponse(request, now), null, null);
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
            return (null, "PURCHASE_REQUEST_NOT_FOUND", "???????");
        }

        if (!string.Equals(request.SellerId, currentUserId, StringComparison.Ordinal))
        {
            return (null, "PURCHASE_REQUEST_ACCESS_DENIED", "??????????");
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
                return (null, "LISTING_NOT_FOUND", "?????");
            }

            if (!string.Equals(listing.SellerId, request.SellerId, StringComparison.Ordinal))
            {
                return (null, "PURCHASE_REQUEST_SELLER_MISMATCH", "???????????????");
            }

            var currentStatus = (ListingStatus)listing.Status;
            if (currentStatus != ListingStatus.Reserved
                && !ListingStatusRules.CanTransition(currentStatus, ListingStatus.Reserved))
            {
                return (null, "LISTING_INVALID_STATUS_TRANSITION", "????????");
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
                null,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrEmpty(message))
        {
            await systemMessageRealtimePublisher.PublishLatestSystemMessageAsync(request.ConversationId, cancellationToken);
        }
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

        var conversations = await LoadConversationsByIdsAsync(
            expired.Select(x => x.ConversationId),
            cancellationToken);

        foreach (var request in expired)
        {
            request.Status = (int)PurchaseRequestStatus.Expired;
            request.RespondedAt = now;
            request.ResponseReason = "??????????";
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
            expired.Select(x => x.ConversationId),
            cancellationToken);
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
            return (null, "PURCHASE_REQUEST_NOT_FOUND", "???????");
        }

        if (pendingRequest.ExpireAt <= now)
        {
            pendingRequest.Status = (int)PurchaseRequestStatus.Expired;
            pendingRequest.RespondedAt = now;
            pendingRequest.ResponseReason = "??????????";
            await AddSystemMessageAsync(
                pendingRequest.ConversationId,
                pendingRequest.SellerId,
                ExpireRequestSystemMessage,
                now,
                null,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await systemMessageRealtimePublisher.PublishLatestSystemMessageAsync(pendingRequest.ConversationId, cancellationToken);
            return (null, "PURCHASE_REQUEST_EXPIRED", "??????????");
        }

        return (pendingRequest.Id, null, null);
    }

    private async Task<(PurchaseRequest? Request, string? ErrorCode, string? ErrorMessage)> GetCurrentRequestByConversationAsync(
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

        var request = await dbContext.PurchaseRequests
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.ConversationId == conversationId, cancellationToken);
        if (request is null)
        {
            return (null, "PURCHASE_REQUEST_NOT_FOUND", "???????");
        }

        return (request, null, null);
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
            return (null, "CONVERSATION_NOT_FOUND", "?????");
        }

        if (!string.Equals(conversation.Participant1Id, currentUserId, StringComparison.Ordinal)
            && !string.Equals(conversation.Participant2Id, currentUserId, StringComparison.Ordinal))
        {
            return (null, "CONVERSATION_ACCESS_DENIED", "????????");
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
            errorMessage = "??????????";
            return false;
        }

        if (request.ExpireAt <= now)
        {
            request.Status = (int)PurchaseRequestStatus.Expired;
            request.RespondedAt = now;
            request.ResponseReason = "??????????";
            errorCode = "PURCHASE_REQUEST_EXPIRED";
            errorMessage = "??????????";
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

    private static ListingStatus ResolveCompletedListingStatus(ListingEntity listing)
    {
        if (listing.IsTradeable)
        {
            return ListingStatus.GivenOrTraded;
        }

        if (listing.IsFree || listing.IsCharity)
        {
            return ListingStatus.Donated;
        }

        return ListingStatus.Sold;
    }
}
