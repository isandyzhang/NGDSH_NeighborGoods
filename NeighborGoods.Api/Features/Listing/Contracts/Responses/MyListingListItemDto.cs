namespace NeighborGoods.Api.Features.Listing.Contracts;

/// <summary>對齊 Web「我的商品」列表列。</summary>
public sealed record MyListingListItemDto(
    Guid Id,
    string Title,
    int CategoryCode,
    string CategoryName,
    int Price,
    bool IsFree,
    bool IsCharity,
    bool IsTradeable,
    int StatusCode,
    string? MainImageUrl,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
