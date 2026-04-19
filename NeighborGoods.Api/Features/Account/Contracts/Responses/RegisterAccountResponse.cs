namespace NeighborGoods.Api.Features.Account.Contracts.Responses;

public sealed record RegisterAccountResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    string UserId
);
