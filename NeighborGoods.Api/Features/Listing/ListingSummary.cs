namespace NeighborGoods.Api.Features.Listing;

public sealed record ListingSummary(
    Guid Id,
    string Title,
    int Category,
    int Condition,
    int Price,
    int Residence
);
