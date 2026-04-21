namespace NeighborGoods.Api.Features.Account.Contracts.Requests;

public sealed record UpdateLinePreferencesRequest(
    bool MarketingPushEnabled,
    bool PreferenceNewListings,
    bool PreferencePriceDrop,
    bool PreferenceMessageDigest);
