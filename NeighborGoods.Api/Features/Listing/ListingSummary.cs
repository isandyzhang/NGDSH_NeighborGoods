namespace NeighborGoods.Api.Features.Listing;

public sealed record ListingSummary(
    Guid Id,
    string SellerId,
    string Title,
    string SellerDisplayName,
    bool SellerEmailVerified,
    bool SellerEmailNotificationEnabled,
    bool SellerLineLoginBound,
    bool SellerLineNotifyBound,
    bool SellerQuickResponder,
    int Category,
    int Condition,
    int Price,
    int Residence,
    int Status,
    bool IsFree,
    bool IsCharity,
    bool IsTradeable,
    bool IsPinned,
    DateTime? PinnedEndDate,
    DateTime? PendingPurchaseRequestExpireAt,
    int? PendingPurchaseRequestRemainingSeconds,
    int InterestCount
);
