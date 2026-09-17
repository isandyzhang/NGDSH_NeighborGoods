using Microsoft.EntityFrameworkCore;
using NeighborGoods.Data;
using NeighborGoods.Data.Listings;

namespace NeighborGoods.Api.Features.Listing.Services;

public sealed class LoginListingExposureService(NeighborGoodsDbContext dbContext)
{
    public async Task TryBoostOldestAsync(
        string userId,
        DateTime? previousLastLoginAt,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var cooldown = TimeSpan.FromHours(ListingConstants.LoginExposureCooldownHours);
        if (previousLastLoginAt.HasValue && nowUtc - previousLastLoginAt.Value < cooldown)
        {
            return;
        }

        var active = (int)ListingStatus.Active;
        var listing = await dbContext.Listings
            .Where(x => x.SellerId == userId && x.Status == active)
            .Where(x => !x.IsPinned
                || !x.PinnedEndDate.HasValue
                || x.PinnedEndDate.Value < nowUtc)
            .OrderBy(x => x.LastExposedAt ?? x.ListedAt)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (listing is null)
        {
            return;
        }

        var minAge = TimeSpan.FromHours(ListingConstants.LoginExposureMinListingAgeHours);
        var effectiveListedAt = listing.LastExposedAt ?? listing.ListedAt;
        if (nowUtc - effectiveListedAt < minAge)
        {
            return;
        }

        listing.LastExposedAt = nowUtc;
        listing.UpdatedAt = nowUtc;
    }
}
