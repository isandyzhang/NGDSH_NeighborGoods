namespace NeighborGoods.Api.Features.Admin.Contracts.Responses;

public sealed record AdminListingDetailResponse(
    Guid Id,
    string Title,
    string Description,
    int CategoryCode,
    int ConditionCode,
    int Price,
    int ResidenceCode,
    int PickupLocationCode,
    bool IsFree,
    bool IsCharity,
    bool IsTradeable,
    int Status,
    string SellerId,
    string SellerDisplayName,
    IReadOnlyList<AdminListingImageResponse> Images
);

public sealed record AdminListingImageResponse(
    Guid Id,
    string ImageUrl,
    int SortOrder
);
