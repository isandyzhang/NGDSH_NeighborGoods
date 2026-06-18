namespace NeighborGoods.Data.Listings;

public static class ListingTopPinRules
{
    public static bool IsEffectivelyPinned(bool isPinned, DateTime? pinnedEndDate, DateTime utcNow) =>
        isPinned && pinnedEndDate.HasValue && pinnedEndDate.Value >= utcNow;
}
