namespace NeighborGoods.Data.Listings;

public sealed class ListingPickupLocation : IListingLookup
{
    public int Id { get; set; }
    public string CodeKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int? ResidenceId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ListingResidence? Residence { get; set; }
    public ICollection<Listing> Listings { get; set; } = new List<Listing>();
}
