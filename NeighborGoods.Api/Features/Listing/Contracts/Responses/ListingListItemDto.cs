namespace NeighborGoods.Api.Features.Listing.Contracts;

public sealed record ListingListItemDto(
    Guid Id,
    ListingListSellerInfoDto Seller,
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
    DateTime? AutoExpiredAt,
    DateTime? PendingPurchaseRequestExpireAt,
    int? PendingPurchaseRequestRemainingSeconds,
    bool InProgress,
    int? InProgressStage,
    int InterestCount,
    int FavoriteCount,
    bool IsFavorited
);
