namespace NeighborGoods.Api.Features.Listing.Contracts;

public sealed record ListingSellerInfoDto(
    string Id,
    string DisplayName,
    DateTime RegisteredAt,
    int MemberDays,
    bool EmailVerified,
    bool QuickResponder,
    bool LineBound
);
