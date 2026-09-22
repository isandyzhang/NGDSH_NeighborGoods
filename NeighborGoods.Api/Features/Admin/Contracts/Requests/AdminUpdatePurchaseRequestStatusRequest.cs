namespace NeighborGoods.Api.Features.Admin.Contracts.Requests;

public sealed record AdminUpdatePurchaseRequestStatusRequest(
    int Status,
    string? Reason);
