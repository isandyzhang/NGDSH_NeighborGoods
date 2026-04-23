namespace NeighborGoods.Api.Features.Reviews.Contracts;

public sealed record ReviewDetailDto(
    Guid ReviewId,
    Guid ListingId,
    string SellerId,
    string BuyerId,
    int Rating,
    string? Content,
    DateTime CreatedAt
);

public sealed record PurchaseRequestReviewStatusDto(
    Guid PurchaseRequestId,
    bool CanReview,
    bool Reviewed,
    string? Reason,
    ReviewDetailDto? Review
);
