namespace NeighborGoods.Api.Features.Auth.Services;

public sealed record AuthTokenPair(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    string UserId
);
