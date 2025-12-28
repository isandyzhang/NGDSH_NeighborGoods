using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeighborGoods.Web.Models;
using NeighborGoods.Web.Models.Configuration;

namespace NeighborGoods.Web.Services;

/// <summary>
/// 通知合併服務實作
/// </summary>
public class NotificationMergeService : INotificationMergeService
{
    private readonly IMemoryCache _cache;
    private readonly LineMessagingApiOptions _options;
    private readonly ILogger<NotificationMergeService> _logger;
    private const string CacheKeyPrefix = "PendingNotifications_";

    public NotificationMergeService(
        IMemoryCache cache,
        IOptions<LineMessagingApiOptions> options,
        ILogger<NotificationMergeService> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public void AddNotification(string userId, PendingNotification notification)
    {
        var cacheKey = $"{CacheKeyPrefix}{userId}";
        
        if (_cache.TryGetValue<List<PendingNotification>>(cacheKey, out var existingNotifications))
        {
            // 檢查是否已有相同對話的通知
            var existing = existingNotifications.FirstOrDefault(n => n.ConversationId == notification.ConversationId);
            if (existing != null)
            {
                // 更新現有通知
                existing.MessageCount += notification.MessageCount;
                existing.LastMessageTime = notification.LastMessageTime;
                if (string.IsNullOrEmpty(existing.MessagePreview) && !string.IsNullOrEmpty(notification.MessagePreview))
                {
                    existing.MessagePreview = notification.MessagePreview;
                }
            }
            else
            {
                // 新增新對話的通知
                existingNotifications.Add(notification);
            }
        }
        else
        {
            // 建立新的通知列表
            existingNotifications = new List<PendingNotification> { notification };
        }

        // 儲存到快取（設定過期時間為合併時間窗口 + 1 分鐘緩衝）
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.MergeWindowMinutes + 1)
        };
        _cache.Set(cacheKey, existingNotifications, cacheOptions);
    }

    public List<PendingNotification> GetPendingNotifications(string userId)
    {
        var cacheKey = $"{CacheKeyPrefix}{userId}";
        
        if (_cache.TryGetValue<List<PendingNotification>>(cacheKey, out var notifications))
        {
            return notifications;
        }

        return new List<PendingNotification>();
    }

    public string MergeNotifications(List<PendingNotification> notifications)
    {
        if (notifications == null || !notifications.Any())
        {
            return string.Empty;
        }

        // 計算總訊息數量
        var totalCount = notifications.Sum(n => n.MessageCount);

        // 產生合併後的訊息：「你有 {數量} 則新訊息！」
        return $"你有 {totalCount} 則新訊息！";
    }

    public void ClearNotifications(string userId)
    {
        var cacheKey = $"{CacheKeyPrefix}{userId}";
        _cache.Remove(cacheKey);
    }
}

