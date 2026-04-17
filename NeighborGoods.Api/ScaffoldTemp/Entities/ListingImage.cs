using System;
using System.Collections.Generic;

namespace NeighborGoods.Api.Shared.Persistence.LegacyEntities;

public partial class ListingImage
{
    public Guid Id { get; set; }

    public Guid ListingId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual global::NeighborGoods.Api.Features.Listing.Listing Listing { get; set; } = null!;
}
