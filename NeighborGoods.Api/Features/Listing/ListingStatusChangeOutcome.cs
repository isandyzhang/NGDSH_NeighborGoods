namespace NeighborGoods.Api.Features.Listing;

public readonly record struct ListingStatusChangeOutcome(
    ListingStatusChangeResult Result,
    string? Warning = null);
