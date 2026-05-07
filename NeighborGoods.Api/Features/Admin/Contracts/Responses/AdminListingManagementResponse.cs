namespace NeighborGoods.Api.Features.Admin.Contracts.Responses;

public sealed record AdminListingManagementResponse(
    IReadOnlyList<AdminListingManagementItemResponse> Items,
    AdminListingManagementPaginationResponse Pagination
);

public sealed record AdminListingManagementItemResponse(
    Guid Id,
    string Title,
    string SellerDisplayName,
    int Price,
    bool IsFree,
    int Status,
    bool IsPinned,
    DateTime CreatedAt
);

public sealed record AdminListingManagementPaginationResponse(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
);
