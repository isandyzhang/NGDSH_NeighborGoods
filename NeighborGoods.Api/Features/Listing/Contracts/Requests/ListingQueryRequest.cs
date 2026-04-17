namespace NeighborGoods.Api.Features.Listing.Contracts;

public sealed class ListingQueryRequest
{
    public string? Query { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
