namespace NeighborGoods.Api.Features.Account.Contracts.Requests;

public sealed class VerifyEmailCodeRequest
{
    public string Email { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;
}
