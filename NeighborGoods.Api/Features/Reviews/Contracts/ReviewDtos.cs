namespace NeighborGoods.Api.Features.Reviews.Contracts;

public sealed record ReviewDetailDto(
    Guid ReviewId,
    Guid PurchaseRequestId,
    string ReviewerId,
    Guid ListingId,
    string SellerId,
    string BuyerId,
    int Rating,
    string? Content,
    DateTime CreatedAt
);

/// <summary>同一筆 purchase request 下，買家與賣家各自評價狀態（雙向）。</summary>
public sealed record PurchaseRequestReviewStatusDto(
    Guid PurchaseRequestId,
    bool BuyerCanReview,
    bool BuyerReviewed,
    string? BuyerReviewBlockReason,
    ReviewDetailDto? BuyerReview,
    bool SellerCanReview,
    bool SellerReviewed,
    string? SellerReviewBlockReason,
    ReviewDetailDto? SellerReview,
    /// <summary>目前登入者是否為買家（否則為賣家）。</summary>
    bool ViewerIsBuyer,
    /// <summary>目前登入者是否仍可送出自己的評價。</summary>
    bool ViewerCanReview,
    /// <summary>目前登入者是否已送出評價。</summary>
    bool ViewerReviewed,
    string? ViewerReviewBlockReason,
    ReviewDetailDto? ViewerReview
);
