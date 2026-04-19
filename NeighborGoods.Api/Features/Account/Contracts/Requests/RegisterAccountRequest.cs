namespace NeighborGoods.Api.Features.Account.Contracts.Requests;

public sealed class RegisterAccountRequest
{
    public string UserName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string EmailVerificationCode { get; init; } = string.Empty;
}
