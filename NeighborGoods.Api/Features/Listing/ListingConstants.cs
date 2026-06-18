namespace NeighborGoods.Api.Features.Listing;

using NeighborGoods.Data.Listings;

/// <summary>與 NeighborGoods.Web.Constants.ListingConstants 對齊。</summary>
public static class ListingConstants
{
    public const int MaxActiveListingsPerUser = 10;
    public const int MinSearchTermLength = 2;
    public const int ListingExpiryDays = ListingExpiryConstants.ExpiryDays;
    public const int ListingExpiryBatchSize = ListingExpiryConstants.BatchSize;
}
