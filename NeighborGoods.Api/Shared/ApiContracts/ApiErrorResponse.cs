namespace NeighborGoods.Api.Shared.ApiContracts;

public sealed record ApiErrorResponse(bool Success, ApiErrorBody Error, ApiMeta Meta);

public sealed record ApiErrorBody(string Code, string Message, object? Details = null);
