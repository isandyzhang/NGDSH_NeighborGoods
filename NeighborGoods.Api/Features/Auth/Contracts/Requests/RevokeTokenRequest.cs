namespace NeighborGoods.Api.Features.Auth.Contracts.Requests;

public sealed class RevokeTokenRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}
