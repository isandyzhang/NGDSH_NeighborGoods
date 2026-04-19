using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Security;

namespace NeighborGoods.Api.Features.Listing.Services;

public sealed class ListingStatusService(
    NeighborGoodsDbContext dbContext,
    ICurrentUserContext currentUserContext,
    ListingConversationNotifyService conversationNotifyService)
{
    internal const string ConversationProceedWarning =
        "此商品有相關的對話記錄，建議您透過正常交易流程完成交易，這樣可以建立買賣雙方關聯並進行評價。您確定要直接標記為交易完成嗎？";

    private const string NotifySoldMessagesFailedWarning = "系統訊息寫入失敗，商品狀態已更新。";

    public Task<ListingStatusChangeOutcome> ReserveAsync(Guid id, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(id, ListingStatus.Reserved, cancellationToken);

    public Task<ListingStatusChangeOutcome> ActivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(id, ListingStatus.Active, cancellationToken);

    public Task<ListingStatusChangeOutcome> MarkSoldAsync(Guid id, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(id, ListingStatus.Sold, cancellationToken);

    public Task<ListingStatusChangeOutcome> SetInactiveAsync(Guid id, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(id, ListingStatus.Inactive, cancellationToken);

    public Task<ListingStatusChangeOutcome> MarkDonatedAsync(Guid id, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(id, ListingStatus.Donated, cancellationToken);

    public Task<ListingStatusChangeOutcome> MarkGivenOrTradedAsync(Guid id, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(id, ListingStatus.GivenOrTraded, cancellationToken);

    /// <summary>對齊 Web <c>ReactivateListingAsync</c>：僅 Inactive → Active，且受刊登上限限制。</summary>
    public async Task<ListingStatusChangeOutcome> ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Listings
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return new ListingStatusChangeOutcome(ListingStatusChangeResult.NotFound);
        }

        var userId = currentUserContext.GetRequiredUserId();
        if (!string.Equals(entity.SellerId, userId, StringComparison.Ordinal))
        {
            throw new ListingAccessException(
                "LISTING_ACCESS_DENIED",
                "僅賣家本人可操作此商品",
                StatusCodes.Status403Forbidden);
        }

        if ((ListingStatus)entity.Status != ListingStatus.Inactive)
        {
            return new ListingStatusChangeOutcome(ListingStatusChangeResult.ReactivateInvalidState);
        }

        var active = (int)ListingStatus.Active;
        var activeListingCount = await dbContext.Listings
            .CountAsync(l => l.SellerId == userId && l.Status == active, cancellationToken);

        if (activeListingCount >= ListingConstants.MaxActiveListingsPerUser)
        {
            return new ListingStatusChangeOutcome(ListingStatusChangeResult.MaxActiveListingsReached);
        }

        entity.Status = active;
        entity.UpdatedAt = DateTime.UtcNow;
        dbContext.Listings.Update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ListingStatusChangeOutcome(ListingStatusChangeResult.Success);
    }

    private async Task<ListingStatusChangeOutcome> ChangeStatusAsync(
        Guid id,
        ListingStatus targetStatus,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Listings
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return new ListingStatusChangeOutcome(ListingStatusChangeResult.NotFound);
        }

        var userId = currentUserContext.GetRequiredUserId();
        if (!string.Equals(entity.SellerId, userId, StringComparison.Ordinal))
        {
            throw new ListingAccessException(
                "LISTING_ACCESS_DENIED",
                "僅賣家本人可操作此商品",
                StatusCodes.Status403Forbidden);
        }

        if (!ListingStatusRules.IsValid(entity.Status))
        {
            return new ListingStatusChangeOutcome(ListingStatusChangeResult.InvalidCurrentStatus);
        }

        var fromStatus = (ListingStatus)entity.Status;
        if (!ListingStatusRules.CanTransition(fromStatus, targetStatus))
        {
            return new ListingStatusChangeOutcome(ListingStatusChangeResult.InvalidTransition);
        }

        if (targetStatus == ListingStatus.Donated && !(entity.IsFree || entity.IsCharity))
        {
            return new ListingStatusChangeOutcome(ListingStatusChangeResult.InvalidDonatedListingType);
        }

        if (targetStatus == ListingStatus.GivenOrTraded && !entity.IsTradeable)
        {
            return new ListingStatusChangeOutcome(ListingStatusChangeResult.InvalidTradeListingType);
        }

        string? warning = null;
        if (targetStatus is ListingStatus.Sold or ListingStatus.GivenOrTraded or ListingStatus.Donated)
        {
            var hasConversation = await dbContext.Conversations
                .AnyAsync(c => c.ListingId == id, cancellationToken);
            if (hasConversation)
            {
                warning = ConversationProceedWarning;
            }
        }

        entity.Status = (int)targetStatus;
        entity.UpdatedAt = DateTime.UtcNow;

        dbContext.Listings.Update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (targetStatus == ListingStatus.Sold)
        {
            var notifyOk = await conversationNotifyService.TryNotifyListingSoldAsync(
                id,
                entity.SellerId,
                cancellationToken);
            if (!notifyOk)
            {
                warning = string.IsNullOrEmpty(warning)
                    ? NotifySoldMessagesFailedWarning
                    : $"{warning}\n{NotifySoldMessagesFailedWarning}";
            }
        }

        return new ListingStatusChangeOutcome(ListingStatusChangeResult.Success, warning);
    }
}
