namespace NeighborGoods.Api.Features.Auth.Contracts.Responses;

public sealed record AuthTokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    string UserId
);
