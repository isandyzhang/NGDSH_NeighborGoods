namespace NeighborGoods.Api.Features.Listing.Contracts;

public sealed record ListingListSellerInfoDto(
    string Id,
    string DisplayName,
    bool EmailVerified,
    bool EmailNotificationEnabled,
    bool LineLoginBound,
    bool LineNotifyBound,
    bool QuickResponder
);
