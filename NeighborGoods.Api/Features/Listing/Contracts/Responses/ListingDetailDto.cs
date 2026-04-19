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
    int PickupLocationCode,
    string PickupLocationName,
    string? MainImageUrl,
    IReadOnlyList<string> ImageUrls,
    int StatusCode,
    bool IsFree,
    bool IsCharity,
    bool IsTradeable,
    bool IsPinned,
    DateTime? PinnedStartDate,
    DateTime? PinnedEndDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
