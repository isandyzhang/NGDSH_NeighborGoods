using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Infrastructure.Storage;
using NeighborGoods.Api.Shared.Security;
using NeighborGoods.Data;
using NeighborGoods.Notifications;
using NeighborGoods.Workers.Line;

namespace NeighborGoods.Api.Features.Listing.Services;

public sealed class ListingTransactionGroupShareService(
    NeighborGoodsDbContext dbContext,
    ICurrentUserContext currentUser,
    ILineTransactionGroupService lineTransactionGroupService,
    ILineMessagingQuotaService lineMessagingQuotaService,
    ILineMessageSender lineMessageSender,
    LineFlexMessageBuilder flexMessageBuilder,
    IBlobStorage blobStorage)
{
    private static readonly TimeSpan ShareCooldown = TimeSpan.FromMinutes(10);

    public async Task ShareAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.GetRequiredUserId();
        var listing = await dbContext.Listings
            .Include(x => x.CategoryInfo)
            .Include(x => x.ConditionInfo)
            .Include(x => x.ResidenceInfo)
            .Include(x => x.ListingImages)
            .FirstOrDefaultAsync(x => x.Id == listingId, cancellationToken);

        if (listing is null)
        {
            throw new ListingAccessException("LISTING_NOT_FOUND", "找不到商品", StatusCodes.Status404NotFound);
        }

        if (!string.Equals(listing.SellerId, userId, StringComparison.Ordinal))
        {
            throw new ListingAccessException(
                "LISTING_ACCESS_DENIED",
                "僅賣家本人可分享此商品到交易群組",
                StatusCodes.Status403Forbidden);
        }

        if (listing.LineGroupSharedAt is DateTime sharedAt
            && DateTime.UtcNow - sharedAt < ShareCooldown)
        {
            var remaining = ShareCooldown - (DateTime.UtcNow - sharedAt);
            var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
            throw new ListingAccessException(
                "LINE_GROUP_SHARE_COOLDOWN",
                $"此商品剛分享過，請約 {minutes} 分鐘後再試。",
                StatusCodes.Status429TooManyRequests);
        }

        var activeGroup = await lineTransactionGroupService.GetActiveAsync(cancellationToken);
        if (activeGroup is null || string.IsNullOrWhiteSpace(activeGroup.GroupId))
        {
            throw new ListingAccessException(
                "LINE_TRANSACTION_GROUP_MISSING",
                "交易群組尚未設定。請先把官方帳號加入交易群組，或在群內 @ 官方帳號一次。",
                StatusCodes.Status400BadRequest);
        }

        var memberCount = await lineMessagingQuotaService.GetGroupMemberCountAsync(
            activeGroup.GroupId,
            cancellationToken);
        if (memberCount is null || memberCount <= 0)
        {
            throw new ListingAccessException(
                "LINE_GROUP_MEMBER_COUNT_UNAVAILABLE",
                "無法取得交易群組人數，暫時無法推播。",
                StatusCodes.Status503ServiceUnavailable);
        }

        var quota = await lineMessagingQuotaService.GetCurrentQuotaAsync(cancellationToken);
        if (quota.RemainingCount is int remainingQuota && remainingQuota < memberCount.Value)
        {
            throw new ListingAccessException(
                "LINE_PUSH_QUOTA_INSUFFICIENT",
                $"LINE 推播餘額不足（群組約 {memberCount.Value} 人，剩餘 {remainingQuota}）。",
                StatusCodes.Status429TooManyRequests);
        }

        var imageUrl = listing.ListingImages
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .Select(x => ResolveImageUrl(x.ImageUrl))
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        var priceLabel = listing.IsFree ? "免費" : $"{listing.Price:0} 元";
        var card = flexMessageBuilder.BuildListingShareCard(
            new LineListingShareItem(
                listing.Id,
                listing.Title,
                priceLabel,
                listing.CategoryInfo?.DisplayName ?? "好物",
                listing.ConditionInfo?.DisplayName ?? "未提供",
                listing.ResidenceInfo?.DisplayName ?? "社宅",
                imageUrl));

        var pushResult = await lineMessageSender.TryPushFlexAsync(
            activeGroup.GroupId,
            card.AltText,
            card.Contents,
            cancellationToken,
            billingUnits: memberCount.Value);

        if (!pushResult.Success)
        {
            throw new ListingAccessException(
                "LINE_GROUP_PUSH_FAILED",
                "推播到交易群組失敗，請稍後再試。",
                StatusCodes.Status502BadGateway);
        }

        listing.LineGroupSharedAt = DateTime.UtcNow;
        listing.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private string? ResolveImageUrl(string storedPathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(storedPathOrUrl))
        {
            return null;
        }

        if (storedPathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            storedPathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return storedPathOrUrl;
        }

        return blobStorage.BuildPublicUrl(storedPathOrUrl);
    }
}
