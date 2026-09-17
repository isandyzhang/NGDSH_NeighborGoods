namespace NeighborGoods.Data.Listings;

public sealed class ListingResidence : IListingLookup
{
    public int Id { get; set; }
    public string CodeKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Listing> Listings { get; set; } = new List<Listing>();
    public ICollection<ListingPickupLocation> PickupLocations { get; set; } = new List<ListingPickupLocation>();
}
