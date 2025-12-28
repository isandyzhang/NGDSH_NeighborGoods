using NeighborGoods.Web.Models.DTOs;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.ViewModels;

namespace NeighborGoods.Web.Services;

/// <summary>
/// 用戶服務介面
/// </summary>
public interface IUserService
{
    /// <summary>
    /// 取得用戶統計數據
    /// </summary>
    Task<UserStatistics> GetUserStatisticsAsync(string userId);

    /// <summary>
    /// 註冊新用戶
    /// </summary>
    Task<ServiceResult<ApplicationUser>> RegisterUserAsync(RegisterViewModel model);

    /// <summary>
    /// 刪除用戶
    /// </summary>
    Task<ServiceResult> DeleteUserAsync(string userId);

    /// <summary>
    /// 綁定 LINE Messaging API User ID
    /// </summary>
    Task<ServiceResult> BindLineMessagingApiAsync(string userId, string lineUserId);

    /// <summary>
    /// 解除 LINE Messaging API 綁定
    /// </summary>
    Task<ServiceResult> UnbindLineMessagingApiAsync(string userId);

    /// <summary>
    /// 更新通知偏好設定
    /// </summary>
    Task<ServiceResult> UpdateNotificationPreferenceAsync(string userId, int preference);

    /// <summary>
    /// 取得 LINE Messaging API 綁定狀態
    /// </summary>
    Task<bool> GetUserLineMessagingApiStatusAsync(string userId);
}

