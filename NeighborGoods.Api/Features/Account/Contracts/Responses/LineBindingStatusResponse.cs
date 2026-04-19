namespace NeighborGoods.Api.Features.Account.Contracts.Responses;

public sealed record LineBindingStatusResponse(
    string Status,
    string Message,
    string? LineUserId = null
);
