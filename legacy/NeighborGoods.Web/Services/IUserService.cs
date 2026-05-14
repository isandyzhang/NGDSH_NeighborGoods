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
    /// 取得 LINE Messaging API 綁定狀態
    /// </summary>
    Task<bool> GetUserLineMessagingApiStatusAsync(string userId);

    /// <summary>
    /// 根據 LINE Messaging API User ID 查詢用戶
    /// </summary>
    Task<ApplicationUser?> GetUserByLineMessagingApiUserIdAsync(string lineUserId);

    /// <summary>
    /// 取得綁定暫存記錄
    /// </summary>
    Task<Models.Entities.LineBindingPending?> GetLineBindingPendingByUserIdAsync(string userId, Guid? pendingBindingId);

    /// <summary>
    /// 根據 LINE User ID 查詢暫存記錄（LineUserId 為 null 的記錄）
    /// </summary>
    Task<List<Models.Entities.LineBindingPending>> GetLineBindingPendingByLineUserIdAsync(string? lineUserId);

    /// <summary>
    /// 建立綁定暫存記錄
    /// </summary>
    Task<ServiceResult<Models.Entities.LineBindingPending>> CreateLineBindingPendingAsync(string userId, string token);

    /// <summary>
    /// 更新暫存記錄的 LINE User ID
    /// </summary>
    Task<ServiceResult> UpdateLineBindingPendingLineUserIdAsync(Guid pendingId, string lineUserId);

    /// <summary>
    /// 刪除綁定暫存記錄
    /// </summary>
    Task<ServiceResult> DeleteLineBindingPendingAsync(Guid pendingId);

    /// <summary>
    /// 檢查 LINE User ID 是否已被其他用戶使用
    /// </summary>
    Task<bool> CheckLineUserIdExistsAsync(string lineUserId, string excludeUserId);

    /// <summary>
    /// 啟用 Email 通知
    /// </summary>
    Task<ServiceResult> EnableEmailNotificationAsync(string userId);

    /// <summary>
    /// 停用 Email 通知
    /// </summary>
    Task<ServiceResult> DisableEmailNotificationAsync(string userId);

    /// <summary>
    /// 取得 Email 通知啟用狀態
    /// </summary>
    Task<bool> GetUserEmailNotificationStatusAsync(string userId);

    /// <summary>
    /// 設定用戶 Email 並啟用通知（不需要驗證）
    /// </summary>
    Task<ServiceResult> SetEmailAndEnableNotificationAsync(string userId, string email);
}

