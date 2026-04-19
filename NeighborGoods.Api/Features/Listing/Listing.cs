using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Features.Listing;

public sealed class Listing
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsFree { get; set; }
    public bool IsCharity { get; set; }
    public string SellerId { get; set; } = string.Empty;
    public int Category { get; set; }
    public ListingCategory? CategoryInfo { get; set; }
    public int PickupLocation { get; set; } = 3;
    public ListingPickupLocation? PickupLocationInfo { get; set; }
    public int Condition { get; set; }
    public ListingCondition? ConditionInfo { get; set; }
    public string? BuyerId { get; set; }
    public int Residence { get; set; }
    public ListingResidence? ResidenceInfo { get; set; }
    public bool IsTradeable { get; set; }
    public bool IsPinned { get; set; }
    public DateTime? PinnedEndDate { get; set; }
    public DateTime? PinnedStartDate { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public AspNetUser? Buyer { get; set; }
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    public ICollection<ListingImage> ListingImages { get; set; } = new List<ListingImage>();
    public ICollection<ListingTopSubmission> ListingTopSubmissions { get; set; } = new List<ListingTopSubmission>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public AspNetUser Seller { get; set; } = null!;
}
