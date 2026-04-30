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
    DateTime UpdatedAt,
    /// <summary>完成態時，對應之已接受購買請求（供評價連結）；可能為 null（例如無正式 PR 即標記售出）。</summary>
    Guid? PurchaseRequestId = null,
    string? BuyerDisplayName = null,
    bool BuyerReviewCompleted = false,
    bool SellerReviewCompleted = false
);
