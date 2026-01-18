namespace NeighborGoods.Web.Models.Configuration;

/// <summary>
/// Email 通知設定選項
/// </summary>
public class EmailNotificationOptions
{
    public const string SectionName = "EmailNotification";

    /// <summary>
    /// Azure Communication Services 連線字串（必填）
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 發送者 Email 地址（必填）
    /// 必須是已驗證的網域中的 Email 地址
    /// </summary>
    public string FromEmailAddress { get; set; } = string.Empty;

    /// <summary>
    /// 發送者顯示名稱（可選）
    /// </summary>
    public string FromDisplayName { get; set; } = "NeighborGoods";
}
