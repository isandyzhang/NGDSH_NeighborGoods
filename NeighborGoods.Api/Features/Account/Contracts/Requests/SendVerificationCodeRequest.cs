namespace NeighborGoods.Api.Features.Account.Contracts.Requests;

public sealed class SendVerificationCodeRequest
{
    public string Email { get; init; } = string.Empty;
}
