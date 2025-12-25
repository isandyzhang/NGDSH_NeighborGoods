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
}


