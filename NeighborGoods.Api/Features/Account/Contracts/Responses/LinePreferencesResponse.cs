namespace NeighborGoods.Api.Features.Account.Contracts.Responses;

public sealed record LinePreferencesResponse(
    bool MarketingPushEnabled,
    bool PreferenceNewListings,
    bool PreferencePriceDrop,
    bool PreferenceMessageDigest,
    DateTime? LastPreferencePushSentAt);
