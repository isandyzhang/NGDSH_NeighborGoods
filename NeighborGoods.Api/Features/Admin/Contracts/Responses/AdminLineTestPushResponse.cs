namespace NeighborGoods.Api.Features.Admin.Contracts.Responses;

public sealed record AdminLineTestPushResponse(
    bool Success,
    string Code,
    string Message,
    bool ChannelAccessTokenConfigured,
    bool LineBound,
    string? LineMessagingApiUserIdSuffix,
    int? HttpStatusCode,
    string? ProviderResponse);
