using NeighborGoods.Web.Models.Enums;

namespace NeighborGoods.Web.Services;

/// <summary>
/// LINE Messaging API 服務介面
/// </summary>
public interface ILineMessagingApiService
{
    /// <summary>
    /// 發送推播訊息給用戶
    /// </summary>
    /// <param name="userId">LINE User ID</param>
    /// <param name="message">訊息內容</param>
    /// <param name="priority">優先級</param>
    Task SendPushMessageAsync(string userId, string message, NotificationPriority priority);

    /// <summary>
    /// 發送帶連結的推播訊息給用戶
    /// </summary>
    /// <param name="userId">LINE User ID</param>
    /// <param name="message">訊息內容</param>
    /// <param name="linkUrl">連結網址</param>
    /// <param name="linkText">連結文字</param>
    /// <param name="priority">優先級</param>
    Task SendPushMessageWithLinkAsync(string userId, string message, string linkUrl, string linkText, NotificationPriority priority);

    /// <summary>
    /// 驗證 Webhook 簽章
    /// </summary>
    /// <param name="body">請求內容</param>
    /// <param name="signature">簽章（從 Header 取得）</param>
    /// <returns>驗證是否通過</returns>
    bool ValidateWebhookSignature(string body, string signature);

    /// <summary>
    /// 解析 Webhook 事件
    /// </summary>
    /// <param name="body">請求內容（JSON）</param>
    /// <returns>事件列表</returns>
    List<LineWebhookEvent> ParseWebhookEvents(string body);
}

/// <summary>
/// LINE Webhook 事件
/// </summary>
public class LineWebhookEvent
{
    public string Type { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object>? Data { get; set; }
}

