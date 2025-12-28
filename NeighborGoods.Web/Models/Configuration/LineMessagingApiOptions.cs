namespace NeighborGoods.Web.Models.Configuration;

/// <summary>
/// LINE Messaging API 設定選項
/// </summary>
public class LineMessagingApiOptions
{
    public const string SectionName = "LineMessagingApi";

    /// <summary>
    /// Channel Access Token（必填，用於發送推播訊息）
    /// </summary>
    public string ChannelAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Channel Secret（必填，用於驗證 Webhook 簽章）
    /// </summary>
    public string ChannelSecret { get; set; } = string.Empty;

    /// <summary>
    /// Channel ID（可選，用於顯示或記錄）
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// 合併時間窗口（分鐘，預設：5）
    /// </summary>
    public int MergeWindowMinutes { get; set; } = 5;

    /// <summary>
    /// 是否啟用通知合併（預設：true）
    /// </summary>
    public bool EnableNotificationMerging { get; set; } = true;
}

