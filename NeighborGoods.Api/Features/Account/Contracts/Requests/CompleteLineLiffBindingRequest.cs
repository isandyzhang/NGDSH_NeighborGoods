namespace NeighborGoods.Api.Features.Account.Contracts.Requests;

public sealed class CompleteLineLiffBindingRequest
{
    public string BindingToken { get; init; } = string.Empty;

    public string IdToken { get; init; } = string.Empty;
}
