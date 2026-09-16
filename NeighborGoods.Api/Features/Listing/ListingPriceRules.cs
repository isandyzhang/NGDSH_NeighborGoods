namespace NeighborGoods.Api.Features.Listing;

public static class ListingPriceRules
{
    /// <summary>
    /// 價格為 0 視為免費；勾選免費時價格歸零。兩者同時出現時以免費為準。
    /// </summary>
    public static (decimal Price, bool IsFree) Normalize(int requestPrice, bool requestIsFree)
    {
        if (requestIsFree || requestPrice == 0)
        {
            return (0, true);
        }

        return (requestPrice, false);
    }
}
