using NeighborGoods.Web.Models.Enums;

namespace NeighborGoods.Web.Models.ViewModels;

public class AdminListingsViewModel
{
    public List<AdminListingItemViewModel> Listings { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class AdminListingItemViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public ListingStatus Status { get; set; }
    public string SellerId { get; set; } = string.Empty;
    public string SellerDisplayName { get; set; } = string.Empty;
    public string? BuyerId { get; set; }
    public string? BuyerDisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
}

