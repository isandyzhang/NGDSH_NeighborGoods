namespace NeighborGoods.Data.Listings;

public interface IListingLookup
{
    int Id { get; }
    string DisplayName { get; }
    int SortOrder { get; }
    bool IsActive { get; }
}
