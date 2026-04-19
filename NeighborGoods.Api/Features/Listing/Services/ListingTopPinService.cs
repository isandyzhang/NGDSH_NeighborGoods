using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Security;

namespace NeighborGoods.Api.Features.Listing.Services;

public sealed class ListingTopPinService(NeighborGoodsDbContext dbContext, ICurrentUserContext currentUserContext)
{
    public async Task UseTopPinAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        var userId = currentUserContext.GetRequiredUserId();
        var listing = await dbContext.Listings
            .FirstOrDefaultAsync(x => x.Id == listingId, cancellationToken);
        if (listing is null)
        {
            throw new ListingAccessException("LISTING_NOT_FOUND", "找不到商品", StatusCodes.Status404NotFound);
        }

        if (!string.Equals(listing.SellerId, userId, StringComparison.Ordinal))
        {
            throw new ListingAccessException("LISTING_ACCESS_DENIED", "僅賣家本人可操作此商品", StatusCodes.Status403Forbidden);
        }

        var user = await dbContext.AspNetUsers.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            throw new ListingAccessException("AUTH_USER_NOT_FOUND", "找不到登入使用者", StatusCodes.Status401Unauthorized);
        }

        if (user.TopPinCredits <= 0)
        {
            throw new ListingAccessException("LISTING_TOP_PIN_NO_CREDITS", "您沒有可用的置頂次數", StatusCodes.Status400BadRequest);
        }

        var now = DateTime.UtcNow;
        if (listing.IsPinned && listing.PinnedEndDate.HasValue && listing.PinnedEndDate.Value >= now)
        {
            throw new ListingAccessException("LISTING_TOP_PIN_ALREADY_ACTIVE", "此商品已經在置頂中", StatusCodes.Status400BadRequest);
        }

        user.TopPinCredits -= 1;
        listing.IsPinned = true;
        listing.PinnedStartDate = now;
        listing.PinnedEndDate = now.AddDays(7);
        listing.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task EndTopPinAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        var userId = currentUserContext.GetRequiredUserId();
        var listing = await dbContext.Listings
            .FirstOrDefaultAsync(x => x.Id == listingId, cancellationToken);
        if (listing is null)
        {
            throw new ListingAccessException("LISTING_NOT_FOUND", "找不到商品", StatusCodes.Status404NotFound);
        }

        if (!string.Equals(listing.SellerId, userId, StringComparison.Ordinal))
        {
            throw new ListingAccessException("LISTING_ACCESS_DENIED", "僅賣家本人可操作此商品", StatusCodes.Status403Forbidden);
        }

        var now = DateTime.UtcNow;
        listing.IsPinned = false;
        listing.PinnedEndDate = now;
        listing.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
