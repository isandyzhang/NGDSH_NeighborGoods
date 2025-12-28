using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Models.Entities;

/// <summary>
/// LINE Bot 綁定暫存記錄
/// </summary>
public class LineBindingPending
{
    /// <summary>
    /// 主鍵
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 網站用戶 ID（外鍵到 AspNetUsers.Id）
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 綁定 Token（GUID 無連字號格式，32 字元）
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// LINE User ID（Webhook 收到 follow 事件時填入）
    /// </summary>
    public string? LineUserId { get; set; }

    /// <summary>
    /// 建立時間（台灣時間）
    /// </summary>
    public DateTime CreatedAt { get; set; } = TaiwanTime.Now;

    /// <summary>
    /// 導航屬性：對應的用戶
    /// </summary>
    public ApplicationUser? User { get; set; }
}

