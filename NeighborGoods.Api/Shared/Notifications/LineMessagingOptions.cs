namespace NeighborGoods.Api.Shared.Notifications;

public sealed class LineMessagingOptions
{
    public const string SectionName = "LineMessagingApi";

    public string ChannelAccessToken { get; set; } = string.Empty;

    public string ChannelSecret { get; set; } = string.Empty;

    public string BotId { get; set; } = "@559fslxw";

    public string BaseUrl { get; set; } = "https://api.line.me/v2/bot";
}
