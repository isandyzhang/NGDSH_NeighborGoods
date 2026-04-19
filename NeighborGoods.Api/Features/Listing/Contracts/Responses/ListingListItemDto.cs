namespace NeighborGoods.Api.Features.Listing.Contracts;

public sealed record ListingListItemDto(
    Guid Id,
    string Title,
    int CategoryCode,
    string CategoryName,
    int ConditionCode,
    string ConditionName,
    int Price,
    int ResidenceCode,
    string ResidenceName,
    string? MainImageUrl,
    int StatusCode,
    bool IsFree,
    bool IsCharity,
    bool IsTradeable,
    bool IsPinned,
    DateTime? PinnedEndDate,
    int InterestCount
);
