namespace NeighborGoods.Api.Features.Listing;

public static class ListingStatusRules
{
    public static bool IsValid(int statusCode) =>
        Enum.IsDefined(typeof(ListingStatus), statusCode);

    /// <summary>
    /// 與 Web <c>UpdateListingStatusAsync</c> 一致：僅上架中／保留中可變更，且目標狀態須在允許集合內。
    /// </summary>
    public static bool CanTransition(ListingStatus from, ListingStatus to)
    {
        if (from == to)
        {
            return false;
        }

        if (from is not (ListingStatus.Active or ListingStatus.Reserved))
        {
            return false;
        }

        return from switch
        {
            ListingStatus.Active => to is ListingStatus.Reserved
                or ListingStatus.Sold
                or ListingStatus.Donated
                or ListingStatus.GivenOrTraded
                or ListingStatus.Inactive,
            ListingStatus.Reserved => to is ListingStatus.Active
                or ListingStatus.Sold
                or ListingStatus.Donated
                or ListingStatus.GivenOrTraded
                or ListingStatus.Inactive,
            _ => false
        };
    }
}
