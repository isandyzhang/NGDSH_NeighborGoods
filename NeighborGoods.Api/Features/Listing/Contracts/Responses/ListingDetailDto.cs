namespace NeighborGoods.Api.Features.Listing.Contracts;

public sealed record ListingDetailDto(
    Guid Id,
    string Title,
    string? Description,
    int CategoryCode,
    string CategoryName,
    int ConditionCode,
    string ConditionName,
    int Price,
    int ResidenceCode,
    string ResidenceName,
    int StatusCode,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
