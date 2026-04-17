using System;
using System.Collections.Generic;

namespace NeighborGoods.Api.Shared.Persistence.LegacyEntities;

public partial class Review
{
    public Guid Id { get; set; }

    public Guid ListingId { get; set; }

    public string SellerId { get; set; } = null!;

    public string BuyerId { get; set; } = null!;

    public int Rating { get; set; }

    public string? Content { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual AspNetUser Buyer { get; set; } = null!;

    public virtual global::NeighborGoods.Api.Features.Listing.Listing Listing { get; set; } = null!;

    public virtual AspNetUser Seller { get; set; } = null!;
}
