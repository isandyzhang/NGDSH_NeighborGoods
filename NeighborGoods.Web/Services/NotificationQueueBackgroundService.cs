using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeighborGoods.Web.Constants;
using NeighborGoods.Web.Data;
using NeighborGoods.Web.Models.Configuration;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Services;

/// <summary>
/// 通知佇列背景服務，定期查詢資料庫並發送 LINE 通知
/// </summary>
public class NotificationQueueBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationQueueBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public NotificationQueueBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<NotificationQueueBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("通知佇列背景服務已啟動");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNotificationQueueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "處理通知佇列時發生錯誤");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("通知佇列背景服務已停止");
    }

    private async Task ProcessNotificationQueueAsync(CancellationToken cancellationToken)
    {
        if (!NotificationConstants.EnableNotificationMerging)
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var messagingService = scope.ServiceProvider.GetRequiredService<ILineMessagingApiService>();
        var db = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();
        var lineOptions = scope.ServiceProvider.GetRequiredService<IOptions<LineMessagingApiOptions>>().Value;

        var now = TaiwanTime.Now;
        var thresholdTime = now.AddMinutes(-NotificationConstants.MergeWindowMinutes); // 測試用：1 分鐘，正式環境改為 -30

        // 一次查詢找出「有新的未讀訊息」的用戶
        // 條件：
        // 1. 未讀訊息超過時間閾值（測試用改為 1 分鐘）
        // 2. 必須有「新的未讀訊息」（相對於上次通知時間）
        var usersToNotify = await (
            from u in db.Users
            where !string.IsNullOrEmpty(u.LineMessagingApiUserId)
            from c in db.Conversations
            where c.Participant1Id == u.Id || c.Participant2Id == u.Id
            from m in db.Messages
            where m.ConversationId == c.Id
                && m.CreatedAt <= thresholdTime  // 超過時間閾值
                && m.SenderId != u.Id  // 不是自己發的
                // 檢查是否未讀
                && ((c.Participant1Id == u.Id && (c.Participant1LastReadAt == null || m.CreatedAt > c.Participant1LastReadAt.Value))
                    || (c.Participant2Id == u.Id && (c.Participant2LastReadAt == null || m.CreatedAt > c.Participant2LastReadAt.Value)))
                // 檢查是否有新的未讀訊息（相對於上次通知時間）
                && (u.LineNotificationLastSentAt == null || m.CreatedAt > u.LineNotificationLastSentAt.Value)
            select new
            {
                UserId = u.Id,
                LineMessagingApiUserId = u.LineMessagingApiUserId,
                ConversationId = c.Id
            }
        )
        .Distinct()
        .ToListAsync(cancellationToken);

        // 對每個用戶發送通知
        foreach (var userInfo in usersToNotify.GroupBy(x => x.UserId))
        {
            var userId = userInfo.Key;
            var lineUserId = userInfo.First().LineMessagingApiUserId;
            var firstConversationId = userInfo.First().ConversationId;

            try
            {
                var user = await db.Users.FindAsync(new object[] { userId }, cancellationToken);
                if (user == null)
                {
                    continue;
                }

                // 發送通知
                var chatUrl = $"/Message/Chat?conversationId={firstConversationId}";
                var baseUrl = lineOptions.BaseUrl?.TrimEnd('/') ?? "https://NeighborGoods.azurewebsites.net";
                var fullUrl = $"{baseUrl}{chatUrl}";

                await messagingService.SendPushMessageWithLinkAsync(
                    lineUserId!,
                    "你有尚未讀取的新訊息",
                    fullUrl,
                    "查看訊息",
                    NotificationPriority.Medium);

                // 更新最後通知時間
                user.LineNotificationLastSentAt = now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "處理用戶 {UserId} 的通知時發生錯誤", userId);
            }
        }

        // 批次儲存所有更新
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "儲存通知時間更新時發生錯誤");
        }
    }
}

