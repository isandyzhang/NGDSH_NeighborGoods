namespace NeighborGoods.Api.Features.Listing;

public static class ListingStatusRules
{
    public static bool IsValid(int statusCode)
    {
        return Enum.IsDefined(typeof(ListingStatus), statusCode);
    }

    public static bool CanTransition(ListingStatus from, ListingStatus to)
    {
        if (from == to)
        {
            return false;
        }

        return (from, to) switch
        {
            (ListingStatus.Active, ListingStatus.Reserved) => true,
            (ListingStatus.Active, ListingStatus.Sold) => true,
            (ListingStatus.Active, ListingStatus.Archived) => true,
            (ListingStatus.Reserved, ListingStatus.Active) => true,
            (ListingStatus.Reserved, ListingStatus.Sold) => true,
            (ListingStatus.Reserved, ListingStatus.Archived) => true,
            (ListingStatus.Sold, ListingStatus.Archived) => true,
            (ListingStatus.Archived, ListingStatus.Active) => true,
            _ => false
        };
    }
}
