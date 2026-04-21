using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Features.Listing;

public sealed class ListingFavorite
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int CategorySnapshot { get; set; }
    public DateTime CreatedAt { get; set; }

    public Listing Listing { get; set; } = null!;
    public AspNetUser User { get; set; } = null!;
}
