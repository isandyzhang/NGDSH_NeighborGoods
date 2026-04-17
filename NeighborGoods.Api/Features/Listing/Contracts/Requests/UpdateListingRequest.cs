namespace NeighborGoods.Api.Features.Listing.Contracts;

public sealed class UpdateListingRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int CategoryCode { get; init; }
    public int ConditionCode { get; init; }
    public int Price { get; init; }
    public int ResidenceCode { get; init; }
}
