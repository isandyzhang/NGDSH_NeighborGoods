using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Listing;
using NeighborGoods.Data;
using NeighborGoods.Data.Listings;
using NeighborGoods.Api.Shared.Security;

namespace NeighborGoods.Api.Features.Listing.Services;

public sealed class ListingRenewalService(
    NeighborGoodsDbContext dbContext,
    ICurrentUserContext currentUserContext,
    ListingConversationNotifyService conversationNotifyService)
{
    internal const string ConversationProceedWarning =
        "此商品有相關的對話記錄，建議您透過正常交易流程完成交易，這樣可以建立買賣雙方關聯並進行評價。您確定要直接標記為交易完成嗎？";

    private const string NotifySoldMessagesFailedWarning = "系統訊息寫入失敗，商品狀態已更新。";

    public async Task<ListingStatusChangeOutcome> RenewAsync(Guid id, CancellationToken cancellationToken = default)
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
            return new ListingStatusChangeOutcome(ListingStatusChangeResult.RenewInvalidState);
        }

        var active = (int)ListingStatus.Active;
        var activeListingCount = await dbContext.Listings
            .CountAsync(l => l.SellerId == userId && l.Status == active, cancellationToken);

        if (activeListingCount >= ListingConstants.MaxActiveListingsPerUser)
        {
            return new ListingStatusChangeOutcome(ListingStatusChangeResult.MaxActiveListingsReached);
        }

        var now = DateTime.UtcNow;
        entity.Status = active;
        entity.ListedAt = now;
        entity.AutoExpiredAt = null;
        entity.UpdatedAt = now;

        dbContext.Listings.Update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ListingStatusChangeOutcome(ListingStatusChangeResult.Success);
    }

    public async Task<ListingStatusChangeOutcome> MarkSoldFromAutoExpiredAsync(
        Guid id,
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

        if ((ListingStatus)entity.Status != ListingStatus.Inactive || !entity.AutoExpiredAt.HasValue)
        {
            return new ListingStatusChangeOutcome(ListingStatusChangeResult.MarkSoldFromExpiryInvalidState);
        }

        string? warning = null;
        var hasConversation = await dbContext.Conversations
            .AnyAsync(c => c.ListingId == id, cancellationToken);
        if (hasConversation)
        {
            warning = ConversationProceedWarning;
        }

        var now = DateTime.UtcNow;
        entity.Status = (int)ListingStatus.Sold;
        entity.AutoExpiredAt = null;
        entity.UpdatedAt = now;

        dbContext.Listings.Update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

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

        return new ListingStatusChangeOutcome(ListingStatusChangeResult.Success, warning);
    }
}
