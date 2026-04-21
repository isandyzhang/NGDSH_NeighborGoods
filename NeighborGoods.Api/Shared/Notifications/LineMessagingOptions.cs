namespace NeighborGoods.Api.Shared.Notifications;

public sealed class LineMessagingOptions
{
    public const string SectionName = "LineMessagingApi";

    public string ChannelAccessToken { get; set; } = string.Empty;

    public string ChannelSecret { get; set; } = string.Empty;

    public string BotId { get; set; } = "@559fslxw";

    public string BaseUrl { get; set; } = "https://api.line.me/v2/bot";

    public string WebBaseUrl { get; set; } = "http://localhost:5173";

    public int PushCooldownMinutes { get; set; } = 720;

    public int MonthlyPushQuota { get; set; } = 200;

    public int PreferencePushSafetyUsagePercent { get; set; } = 70;

    public bool PreferencePushEnabled { get; set; } = false;

    public int PreferencePushIntervalMinutes { get; set; } = 60;

    public int PreferencePushBatchSize { get; set; } = 20;
}
