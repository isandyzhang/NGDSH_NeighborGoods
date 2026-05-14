namespace NeighborGoods.Web.Constants;

/// <summary>
/// 通知相關常數
/// </summary>
public static class NotificationConstants
{
    /// <summary>
    /// 通知合併時間窗口（分鐘）
    /// 測試用：設為 1 分鐘
    /// 正式環境：應改為 30 分鐘
    /// </summary>
    public const int MergeWindowMinutes = 1;

    /// <summary>
    /// 是否啟用通知合併
    /// </summary>
    public const bool EnableNotificationMerging = true;
}

