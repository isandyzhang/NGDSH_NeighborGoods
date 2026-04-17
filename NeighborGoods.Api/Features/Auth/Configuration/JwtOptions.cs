namespace NeighborGoods.Api.Features.Auth.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "NeighborGoods.Api";
    public string Audience { get; init; } = "NeighborGoods.Api.Client";
    public string SigningKey { get; init; } = "neighbor-goods-dev-signing-key-change-in-production";
    public int AccessTokenMinutes { get; init; } = 30;
    public int RefreshTokenDays { get; init; } = 14;
}
