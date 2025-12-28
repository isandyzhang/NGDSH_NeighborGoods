using NeighborGoods.Web.Models;

namespace NeighborGoods.Web.Services;

/// <summary>
/// 通知合併服務介面
/// </summary>
public interface INotificationMergeService
{
    /// <summary>
    /// 加入待合併通知
    /// </summary>
    /// <param name="userId">接收者 ID</param>
    /// <param name="notification">待合併通知</param>
    void AddNotification(string userId, PendingNotification notification);

    /// <summary>
    /// 取得待合併通知
    /// </summary>
    /// <param name="userId">接收者 ID</param>
    /// <returns>待合併通知列表</returns>
    List<PendingNotification> GetPendingNotifications(string userId);

    /// <summary>
    /// 合併通知內容
    /// </summary>
    /// <param name="notifications">待合併的通知列表</param>
    /// <returns>合併後的訊息文字</returns>
    string MergeNotifications(List<PendingNotification> notifications);

    /// <summary>
    /// 清除已處理的通知
    /// </summary>
    /// <param name="userId">接收者 ID</param>
    void ClearNotifications(string userId);
}

