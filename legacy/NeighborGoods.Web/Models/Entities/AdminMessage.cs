using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Models.Entities;

/// <summary>
/// 管理員訊息（用戶發送給管理員的訊息，獨立於 Conversation 系統）
/// </summary>
public class AdminMessage
{
    public Guid Id { get; set; }

    /// <summary>
    /// 發送者使用者 Id（外鍵 → ApplicationUser）
    /// </summary>
    public string SenderId { get; set; } = string.Empty;

    /// <summary>
    /// 發送者的導航屬性
    /// </summary>
    public ApplicationUser? Sender { get; set; }

    /// <summary>
    /// 訊息內容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 是否已讀
    /// </summary>
    public bool IsRead { get; set; } = false;

    /// <summary>
    /// 發送時間（台灣時間）
    /// </summary>
    public DateTime CreatedAt { get; set; } = TaiwanTime.Now;
}

