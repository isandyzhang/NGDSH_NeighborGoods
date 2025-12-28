using NeighborGoods.Web.Models.Enums;

namespace NeighborGoods.Web.Models;

/// <summary>
/// 待合併的通知資料結構
/// </summary>
public class PendingNotification
{
    /// <summary>
    /// 接收者 ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 對話 ID
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// 發送者名稱（用於記錄，但合併通知中不顯示）
    /// </summary>
    public string SenderName { get; set; } = string.Empty;

    /// <summary>
    /// 訊息數量
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    /// 最後訊息時間
    /// </summary>
    public DateTime LastMessageTime { get; set; }

    /// <summary>
    /// 第一則訊息時間
    /// </summary>
    public DateTime FirstMessageTime { get; set; }

    /// <summary>
    /// 優先級
    /// </summary>
    public NotificationPriority Priority { get; set; }

    /// <summary>
    /// 訊息預覽（可選）
    /// </summary>
    public string? MessagePreview { get; set; }
}

