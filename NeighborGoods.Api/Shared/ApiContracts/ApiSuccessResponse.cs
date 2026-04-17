namespace NeighborGoods.Api.Shared.ApiContracts;

public sealed record ApiSuccessResponse<T>(bool Success, T Data, ApiMeta Meta);
