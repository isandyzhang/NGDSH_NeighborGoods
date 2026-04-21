using System;
using System.Collections.Generic;

namespace NeighborGoods.Api.Shared.Persistence.LegacyEntities;

public partial class Listing
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public decimal Price { get; set; }

    public bool IsFree { get; set; }

    public bool IsCharity { get; set; }

    public int Status { get; set; }

    public string SellerId { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Category { get; set; }

    public int PickupLocation { get; set; }

    public int Condition { get; set; }

    public string? BuyerId { get; set; }

    public int Residence { get; set; }

    public bool IsTradeable { get; set; }

    public bool IsPinned { get; set; }

    public DateTime? PinnedEndDate { get; set; }

    public DateTime? PinnedStartDate { get; set; }

    public virtual AspNetUser? Buyer { get; set; }

    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public virtual ICollection<ListingImage> ListingImages { get; set; } = new List<ListingImage>();

    public virtual ICollection<ListingTopSubmission> ListingTopSubmissions { get; set; } = new List<ListingTopSubmission>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<PurchaseRequest> PurchaseRequests { get; set; } = new List<PurchaseRequest>();

    public virtual AspNetUser Seller { get; set; } = null!;
}
