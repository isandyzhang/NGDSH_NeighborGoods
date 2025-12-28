using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NeighborGoods.Web.Constants;
using NeighborGoods.Web.Data;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Services;

/// <summary>
/// 通知佇列背景服務，定期處理待合併的通知
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
        var mergeService = scope.ServiceProvider.GetRequiredService<INotificationMergeService>();
        var messagingService = scope.ServiceProvider.GetRequiredService<ILineMessagingApiService>();
        var db = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();

        // 取得所有待處理的用戶（這裡需要一個機制來追蹤哪些用戶有待處理的通知）
        // 簡化實作：使用資料庫查詢所有已綁定 LINE Bot 的用戶
        var usersWithLineBot = await db.Users
            .Where(u => !string.IsNullOrEmpty(u.LineMessagingApiUserId))
            .Select(u => new { u.Id, u.LineMessagingApiUserId })
            .ToListAsync(cancellationToken);

        var now = TaiwanTime.Now;
        var mergeWindow = TimeSpan.FromMinutes(NotificationConstants.MergeWindowMinutes);

        foreach (var user in usersWithLineBot)
        {
            if (string.IsNullOrEmpty(user.LineMessagingApiUserId))
            {
                continue;
            }

            try
            {
                var pendingNotifications = mergeService.GetPendingNotifications(user.Id);

                if (!pendingNotifications.Any())
                {
                    continue;
                }

                // 檢查是否有超過時間窗口的通知
                var expiredNotifications = pendingNotifications
                    .Where(n => now - n.FirstMessageTime >= mergeWindow)
                    .ToList();

                if (expiredNotifications.Any())
                {
                    // 合併通知
                    var mergedMessage = mergeService.MergeNotifications(expiredNotifications);
                    
                    if (!string.IsNullOrEmpty(mergedMessage))
                    {
                        // 發送合併後的通知
                        var chatUrl = $"/Message/Chat?conversationId={expiredNotifications.First().ConversationId}";
                        var fullUrl = $"https://your-site.azurewebsites.net{chatUrl}"; // TODO: 從設定檔取得基礎 URL

                        await messagingService.SendPushMessageWithLinkAsync(
                            user.LineMessagingApiUserId,
                            mergedMessage,
                            fullUrl,
                            "查看對話",
                            NotificationPriority.Medium);

                        // 清除已處理的通知
                        mergeService.ClearNotifications(user.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "處理用戶 {UserId} 的通知佇列時發生錯誤", user.Id);
            }
        }
    }
}

