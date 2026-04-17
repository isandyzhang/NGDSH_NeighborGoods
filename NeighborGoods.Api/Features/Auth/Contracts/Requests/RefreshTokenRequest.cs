namespace NeighborGoods.Api.Features.Auth.Contracts.Requests;

public sealed class RefreshTokenRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}
