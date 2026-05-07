namespace NeighborGoods.Api.Features.Admin.Contracts.Requests;

public sealed record AdminBatchListingStatusRequest(
    IReadOnlyList<Guid> ListingIds,
    int Status
);
