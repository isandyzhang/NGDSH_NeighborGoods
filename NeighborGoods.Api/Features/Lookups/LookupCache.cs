using Microsoft.Extensions.Caching.Memory;

namespace NeighborGoods.Api.Features.Lookups;

public static class LookupCacheKeys
{
    public const string Conditions = "lookups:v1:conditions";
    public const string Residences = "lookups:v1:residences";
    public const string PickupLocations = "lookups:v1:pickup-locations";
    public const string Categories = "lookups:v1:categories";

    public const string ListingConditions = "listing-lookups:conditions";
    public const string ListingResidences = "listing-lookups:residences";
    public const string ListingPickupLocations = "listing-lookups:pickup-locations";
    public const string ListingCategories = "listing-lookups:categories";

    public static readonly string[] All =
    [
        Conditions,
        Residences,
        PickupLocations,
        Categories,
        ListingConditions,
        ListingResidences,
        ListingPickupLocations,
        ListingCategories,
    ];
}

public sealed class LookupCache(IMemoryCache memoryCache)
{
    public void InvalidateAll()
    {
        foreach (var key in LookupCacheKeys.All)
        {
            memoryCache.Remove(key);
        }
    }
}
