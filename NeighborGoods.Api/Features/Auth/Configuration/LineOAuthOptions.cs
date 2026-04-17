namespace NeighborGoods.Api.Features.Auth.Configuration;

public sealed class LineOAuthOptions
{
    public const string SectionName = "Line";

    public string ChannelId { get; init; } = string.Empty;
    public string ChannelSecret { get; init; } = string.Empty;
    public string CallbackUrl { get; init; } = string.Empty;
    public string Scope { get; init; } = "openid profile";
}
