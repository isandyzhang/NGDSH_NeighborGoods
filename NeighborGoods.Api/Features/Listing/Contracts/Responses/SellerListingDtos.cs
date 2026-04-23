namespace NeighborGoods.Api.Features.Listing.Contracts;

public sealed record SellerSummaryDto(
    string SellerId,
    string SellerDisplayName,
    int TotalListings,
    int ActiveListings,
    int CompletedListings
);

public sealed record SellerListingListItemDto(
    Guid Id,
    string Title,
    int CategoryCode,
    string CategoryName,
    int Price,
    bool IsFree,
    int StatusCode,
    string? MainImageUrl,
    DateTime CreatedAt
);

public sealed record SellerListingsQueryResultDto(
    SellerSummaryDto Seller,
    IReadOnlyList<SellerListingListItemDto> Items,
    int Page,
    int PageSize,
    int Total
);
