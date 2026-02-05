using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.Enums;

namespace NeighborGoods.Web.Models.ViewModels;

public class MyListingsViewModel
{
    public List<ListingItem> Listings { get; set; } = new List<ListingItem>();
}

public class ListingItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public ListingCategory Category { get; set; }
    public decimal Price { get; set; }
    public bool IsFree { get; set; }
    public bool IsCharity { get; set; }
    public bool IsTradeable { get; set; }
    public ListingStatus Status { get; set; }
    public string? FirstImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

