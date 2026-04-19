namespace NeighborGoods.Api.Features.Listing;

public sealed class ListingCondition
{
    public int Id { get; set; }
    public string CodeKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Listing> Listings { get; set; } = new List<Listing>();
}
