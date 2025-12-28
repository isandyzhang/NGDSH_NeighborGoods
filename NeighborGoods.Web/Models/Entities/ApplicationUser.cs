using Microsoft.AspNetCore.Identity;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Models.Entities;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 對應 README 中的 LineUserId（LINE 帳號可為 null）
    /// </summary>
    public string? LineUserId { get; set; }

    /// <summary>
    /// 使用者角色（一般用戶 / 公益團體 / 版主 / 管理員）
    /// </summary>
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>
    /// 使用者建立時間（台灣時間，UTC+8）
    /// </summary>
    public DateTime CreatedAt { get; set; } = TaiwanTime.Now;

    /// <summary>
    /// LINE Messaging API User ID（用於識別 LINE Bot 用戶）
    /// </summary>
    public string? LineMessagingApiUserId { get; set; }

    /// <summary>
    /// LINE Messaging API 授權時間（台灣時間，UTC+8）
    /// </summary>
    public DateTime? LineMessagingApiAuthorizedAt { get; set; }

    /// <summary>
    /// LINE 通知最後發送時間（台灣時間，UTC+8）
    /// 用於防止重複通知，30 分鐘內不重複通知同一用戶
    /// </summary>
    public DateTime? LineNotificationLastSentAt { get; set; }
}


