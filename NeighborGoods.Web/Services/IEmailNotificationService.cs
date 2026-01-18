using NeighborGoods.Web.Models.Enums;

namespace NeighborGoods.Web.Services;

/// <summary>
/// Email 通知服務介面
/// 完全對應 LINE 通知介面，以便未來可以輕鬆取代
/// </summary>
public interface IEmailNotificationService
{
    /// <summary>
    /// 發送推播訊息給用戶（對應 LINE 的 SendPushMessageAsync）
    /// </summary>
    /// <param name="email">用戶 Email 地址</param>
    /// <param name="message">訊息內容</param>
    /// <param name="priority">優先級</param>
    Task SendPushMessageAsync(string email, string message, NotificationPriority priority);

    /// <summary>
    /// 發送帶連結的推播訊息給用戶（對應 LINE 的 SendPushMessageWithLinkAsync）
    /// </summary>
    /// <param name="email">用戶 Email 地址</param>
    /// <param name="message">訊息內容</param>
    /// <param name="linkUrl">連結網址</param>
    /// <param name="linkText">連結文字</param>
    /// <param name="priority">優先級</param>
    Task SendPushMessageWithLinkAsync(string email, string message, string linkUrl, string linkText, NotificationPriority priority);
}
