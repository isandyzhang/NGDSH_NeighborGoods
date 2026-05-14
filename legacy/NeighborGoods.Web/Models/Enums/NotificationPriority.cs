namespace NeighborGoods.Web.Models.Enums;

/// <summary>
/// 通知優先級
/// </summary>
public enum NotificationPriority
{
    /// <summary>
    /// 高優先級：立即通知（購買請求、交易狀態變更）
    /// </summary>
    High = 1,

    /// <summary>
    /// 中優先級：5 分鐘內合併（一般對話訊息）
    /// </summary>
    Medium = 2,

    /// <summary>
    /// 低優先級：定期摘要（非重要訊息）
    /// </summary>
    Low = 3
}

