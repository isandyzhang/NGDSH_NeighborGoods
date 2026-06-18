using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeighborGoods.Data;
using NeighborGoods.Data.Listings;
using NeighborGoods.Notifications;

namespace NeighborGoods.Workers.Listings;

public sealed class ListingExpiryJob(
    NeighborGoodsDbContext dbContext,
    IEmailSender emailSender,
    ILineMessageSender lineMessageSender,
    LinePushPolicyService linePushPolicyService,
    LineFlexMessageBuilder lineFlexMessageBuilder,
    IOptions<LineMessagingOptions> lineMessagingOptions,
    ILogger<ListingExpiryJob> logger)
{
    private const string DefaultWebBaseUrl = "https://www.neighborgoodstw.com";

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiryThreshold = now.AddDays(-ListingExpiryConstants.ExpiryDays);
        var active = (int)ListingStatus.Active;

        var expiredListings = await dbContext.Listings
            .Where(x =>
                x.Status == active
                && x.ListedAt <= expiryThreshold
                && x.ExpiryNoticeSentAt == null)
            .OrderBy(x => x.ListedAt)
            .Take(ListingExpiryConstants.BatchSize)
            .ToListAsync(cancellationToken);

        if (expiredListings.Count == 0)
        {
            await ClearExpiredTopPinsAsync(now, cancellationToken);
            return 0;
        }

        var listingIds = expiredListings.Select(x => x.Id).ToList();
        var coverImages = await GetCoverImageMapAsync(listingIds, cancellationToken);
        var categoryNames = await dbContext.ListingCategories
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        foreach (var listing in expiredListings)
        {
            listing.Status = (int)ListingStatus.Inactive;
            listing.AutoExpiredAt = now;
            listing.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var sellerGroups = expiredListings.GroupBy(x => x.SellerId);
        var webBaseUrl = string.IsNullOrWhiteSpace(lineMessagingOptions.Value.WebBaseUrl)
            ? DefaultWebBaseUrl
            : lineMessagingOptions.Value.WebBaseUrl.TrimEnd('/');

        foreach (var group in sellerGroups)
        {
            var seller = await dbContext.AspNetUsers
                .FirstOrDefaultAsync(x => x.Id == group.Key, cancellationToken);
            if (seller is null)
            {
                continue;
            }

            var payloads = group.Select(x => new ListingExpiryNotifyPayload(
                x.Id,
                x.Title,
                coverImages.GetValueOrDefault(x.Id),
                x.Price,
                x.IsFree,
                categoryNames.GetValueOrDefault(x.Category, "其他"))).ToList();

            var notified = await NotifySellerAsync(seller, payloads, webBaseUrl, now, cancellationToken);
            if (!notified)
            {
                continue;
            }

            foreach (var listing in group)
            {
                listing.ExpiryNoticeSentAt = now;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await ClearExpiredTopPinsAsync(now, cancellationToken);

        logger.LogInformation("Listing expiry job processed {Count} listings.", expiredListings.Count);
        return expiredListings.Count;
    }

    private async Task<bool> NotifySellerAsync(
        Data.LegacyEntities.AspNetUser seller,
        IReadOnlyList<ListingExpiryNotifyPayload> payloads,
        string webBaseUrl,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (linePushPolicyService.CanSendTransactionalPush(seller, now))
        {
            try
            {
                var flexItems = payloads.Select(p => new LineListingExpiryItem(
                    p.ListingId,
                    p.Title,
                    ListingImageUrlHelper.Resolve(p.CoverImageStored),
                    p.Price,
                    p.IsFree,
                    p.CategoryName)).ToList();

                var card = lineFlexMessageBuilder.BuildListingExpiryNotice(flexItems);
                await lineMessageSender.PushFlexAsync(
                    seller.LineMessagingApiUserId!,
                    card.AltText,
                    card.Contents,
                    cancellationToken);
                seller.LineNotificationLastSentAt = now;
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "LINE 刊登到期通知失敗：SellerId={SellerId}", seller.Id);
            }
        }

        if (!CanSendExpiryEmail(seller))
        {
            return false;
        }

        try
        {
            var emailItems = payloads.Select(p => new ListingExpiryEmailItem(
                p.ListingId,
                p.Title,
                ListingImageUrlHelper.Resolve(p.CoverImageStored))).ToList();

            var (subject, plainText, html) = ListingExpiryEmailBuilder.Build(webBaseUrl, emailItems);
            await emailSender.SendAsync(
                seller.Email!,
                subject,
                plainText,
                html,
                cancellationToken: cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Email 刊登到期通知失敗：SellerId={SellerId}", seller.Id);
            return false;
        }
    }

    private static bool CanSendExpiryEmail(Data.LegacyEntities.AspNetUser seller) =>
        seller.EmailNotificationEnabled
        && seller.EmailConfirmed
        && !string.IsNullOrWhiteSpace(seller.Email);

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
            .ToDictionary(g => g.Key, g => g.First().ImageUrl);
    }

    private async Task ClearExpiredTopPinsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var expiredPins = await dbContext.Listings
            .Where(x => x.IsPinned && x.PinnedEndDate.HasValue && x.PinnedEndDate.Value < now)
            .ToListAsync(cancellationToken);

        if (expiredPins.Count == 0)
        {
            return;
        }

        foreach (var listing in expiredPins)
        {
            listing.IsPinned = false;
            listing.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record ListingExpiryNotifyPayload(
        Guid ListingId,
        string Title,
        string? CoverImageStored,
        decimal Price,
        bool IsFree,
        string CategoryName);
}
