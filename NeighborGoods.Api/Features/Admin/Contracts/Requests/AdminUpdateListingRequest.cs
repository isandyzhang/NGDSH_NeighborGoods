namespace NeighborGoods.Api.Features.Admin.Contracts.Requests;

public sealed class AdminUpdateListingRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int CategoryCode { get; init; }
    public int ConditionCode { get; init; }
    public int Price { get; init; }
    public int ResidenceCode { get; init; }
    public int PickupLocationCode { get; init; }
    public bool IsFree { get; init; }
    public bool IsCharity { get; init; }
    public bool IsTradeable { get; init; }
    public IReadOnlyList<string>? ImageUrlsToDelete { get; init; }
    public IReadOnlyList<string>? ImageUrlsInOrder { get; init; }
    public int Status { get; init; }
}
