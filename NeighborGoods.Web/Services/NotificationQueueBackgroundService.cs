using Microsoft.Data.SqlClient;
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
using System.Diagnostics;

namespace NeighborGoods.Web.Services;

/// <summary>
/// 通知佇列背景服務，定期查詢資料庫並發送 LINE 和 Email 通知
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

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var lineMessagingService = scope.ServiceProvider.GetService<ILineMessagingApiService>();
            var emailNotificationService = scope.ServiceProvider.GetService<IEmailNotificationService>();
            var db = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Models.Entities.ApplicationUser>>();

            // 設定查詢超時為 30 秒
            db.Database.SetCommandTimeout(30);

            var now = TaiwanTime.Now;
            var thresholdTime = now.AddMinutes(-NotificationConstants.MergeWindowMinutes);

            _logger.LogDebug("開始處理通知佇列，時間閾值：{ThresholdTime}", thresholdTime);

            // 第一步：找出所有啟用通知的用戶（LINE 或 Email）
            var usersWithNotifications = await db.Users
                .Where(u => (!string.IsNullOrEmpty(u.LineMessagingApiUserId)) || u.EmailNotificationEnabled)
                .Select(u => new
                {
                    u.Id,
                    u.LineMessagingApiUserId,
                    u.LineNotificationLastSentAt,
                    u.EmailNotificationEnabled,
                    u.Email,
                    u.EmailNotificationLastSentAt
                })
                .ToListAsync(cancellationToken);

            if (!usersWithNotifications.Any())
            {
                _logger.LogDebug("沒有啟用通知的用戶，跳過通知處理");
                return;
            }

            _logger.LogDebug("找到 {Count} 個啟用通知的用戶", usersWithNotifications.Count);

            var userIds = usersWithNotifications.Select(u => u.Id).ToList();
            var userLineLastSentDict = usersWithNotifications
                .Where(u => !string.IsNullOrEmpty(u.LineMessagingApiUserId))
                .ToDictionary(u => u.Id, u => u.LineNotificationLastSentAt);
            var userEmailLastSentDict = usersWithNotifications
                .Where(u => u.EmailNotificationEnabled && !string.IsNullOrEmpty(u.Email))
                .ToDictionary(u => u.Id, u => u.EmailNotificationLastSentAt);

            // 第二步：查詢這些用戶參與的對話
            var userConversations = await db.Conversations
                .Where(c => userIds.Contains(c.Participant1Id) || userIds.Contains(c.Participant2Id))
                .Select(c => new
                {
                    c.Id,
                    c.Participant1Id,
                    c.Participant2Id,
                    c.Participant1LastReadAt,
                    c.Participant2LastReadAt
                })
                .ToListAsync(cancellationToken);

            if (!userConversations.Any())
            {
                _logger.LogDebug("沒有找到相關對話，跳過通知處理");
                return;
            }

            var conversationIds = userConversations.Select(c => c.Id).ToList();
            var conversationDict = userConversations.ToDictionary(c => c.Id);

            // 第三步：查詢未讀訊息
            var unreadMessages = await db.Messages
                .Where(m => conversationIds.Contains(m.ConversationId)
                    && m.CreatedAt <= thresholdTime)
                .Select(m => new
                {
                    m.Id,
                    m.ConversationId,
                    m.SenderId,
                    m.CreatedAt
                })
                .ToListAsync(cancellationToken);

            if (!unreadMessages.Any())
            {
                _logger.LogDebug("沒有找到未讀訊息，跳過通知處理");
                return;
            }

            // 第四步：在記憶體中過濾和處理
            var usersToNotify = new Dictionary<string, (bool HasLine, string? LineUserId, bool HasEmail, string? Email, Guid ConversationId)>();

            foreach (var message in unreadMessages)
            {
                if (!conversationDict.TryGetValue(message.ConversationId, out var conversation))
                {
                    continue;
                }

                // 判斷訊息是發給哪個參與者的
                string? targetUserId = null;
                DateTime? lastReadAt = null;

                if (conversation.Participant1Id != message.SenderId)
                {
                    targetUserId = conversation.Participant1Id;
                    lastReadAt = conversation.Participant1LastReadAt;
                }
                else if (conversation.Participant2Id != message.SenderId)
                {
                    targetUserId = conversation.Participant2Id;
                    lastReadAt = conversation.Participant2LastReadAt;
                }

                if (string.IsNullOrEmpty(targetUserId))
                {
                    continue; // 訊息是自己發的，跳過
                }

                // 檢查是否未讀
                if (lastReadAt.HasValue && message.CreatedAt <= lastReadAt.Value)
                {
                    continue; // 已讀，跳過
                }

                // 檢查 LINE 通知：是否有新的未讀訊息（相對於上次通知時間）
                bool shouldNotifyLine = false;
                if (userLineLastSentDict.TryGetValue(targetUserId, out var lineLastSentAt))
                {
                    if (!lineLastSentAt.HasValue || message.CreatedAt > lineLastSentAt.Value)
                    {
                        shouldNotifyLine = true;
                    }
                }
                else if (usersWithNotifications.Any(u => u.Id == targetUserId && !string.IsNullOrEmpty(u.LineMessagingApiUserId)))
                {
                    shouldNotifyLine = true; // 第一次通知
                }

                // 檢查 Email 通知：是否有新的未讀訊息（相對於上次通知時間）
                bool shouldNotifyEmail = false;
                if (userEmailLastSentDict.TryGetValue(targetUserId, out var emailLastSentAt))
                {
                    if (!emailLastSentAt.HasValue || message.CreatedAt > emailLastSentAt.Value)
                    {
                        shouldNotifyEmail = true;
                    }
                }
                else if (usersWithNotifications.Any(u => u.Id == targetUserId && u.EmailNotificationEnabled && !string.IsNullOrEmpty(u.Email)))
                {
                    shouldNotifyEmail = true; // 第一次通知
                }

                // 如果兩種通知都不需要，跳過
                if (!shouldNotifyLine && !shouldNotifyEmail)
                {
                    continue;
                }

                // 記錄需要通知的用戶（每個用戶只記錄第一個對話）
                if (!usersToNotify.ContainsKey(targetUserId))
                {
                    var user = usersWithNotifications.FirstOrDefault(u => u.Id == targetUserId);
                    if (user != null)
                    {
                        usersToNotify[targetUserId] = (
                            HasLine: shouldNotifyLine && !string.IsNullOrEmpty(user.LineMessagingApiUserId),
                            LineUserId: user.LineMessagingApiUserId,
                            HasEmail: shouldNotifyEmail && user.EmailNotificationEnabled && !string.IsNullOrEmpty(user.Email),
                            Email: user.Email,
                            ConversationId: message.ConversationId
                        );
                    }
                }
            }

            if (!usersToNotify.Any())
            {
                _logger.LogDebug("沒有需要通知的用戶");
                return;
            }

            _logger.LogInformation("準備通知 {Count} 個用戶", usersToNotify.Count);

            // 第五步：對每個用戶發送通知
            var lineNotifiedCount = 0;
            var emailNotifiedCount = 0;
            var errorCount = 0;

            foreach (var (userId, (hasLine, lineUserId, hasEmail, email, conversationId)) in usersToNotify)
            {
                try
                {
                    var user = await db.Users.FindAsync(new object[] { userId }, cancellationToken);
                    if (user == null)
                    {
                        _logger.LogWarning("找不到用戶 {UserId}，跳過通知", userId);
                        continue;
                    }

                    // 發送通知的 URL
                    var chatUrl = $"/Message/Chat?conversationId={conversationId}";
                    var fullUrl = $"https://neighborgoods.azurewebsites.net{chatUrl}";
                    var message = "你有尚未讀取的新訊息";
                    var linkText = "查看訊息";

                    // 發送 LINE 通知
                    if (hasLine && lineMessagingService != null && !string.IsNullOrEmpty(lineUserId))
                    {
                        try
                        {
                            await lineMessagingService.SendPushMessageWithLinkAsync(
                                lineUserId,
                                message,
                                fullUrl,
                                linkText,
                                NotificationPriority.Medium);
                            user.LineNotificationLastSentAt = now;
                            lineNotifiedCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "發送 LINE 通知給用戶 {UserId} 時發生錯誤", userId);
                        }
                    }

                    // 發送 Email 通知
                    if (hasEmail && emailNotificationService != null && !string.IsNullOrEmpty(email))
                    {
                        try
                        {
                            // 確認 Email 已驗證
                            if (await userManager.IsEmailConfirmedAsync(user))
                            {
                                await emailNotificationService.SendPushMessageWithLinkAsync(
                                    email,
                                    message,
                                    fullUrl,
                                    linkText,
                                    NotificationPriority.Medium);
                                user.EmailNotificationLastSentAt = now;
                                emailNotifiedCount++;
                            }
                            else
                            {
                                _logger.LogWarning("用戶 {UserId} 的 Email 未驗證，跳過 Email 通知", userId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "發送 Email 通知給用戶 {UserId} 時發生錯誤", userId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogError(ex, "處理用戶 {UserId} 的通知時發生錯誤", userId);
                }
            }

            // 批次儲存所有更新
            if (lineNotifiedCount > 0 || emailNotifiedCount > 0)
            {
                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("成功通知：LINE {LineCount} 個用戶，Email {EmailCount} 個用戶，{ErrorCount} 個失敗", 
                        lineNotifiedCount, emailNotifiedCount, errorCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "儲存通知時間更新時發生錯誤");
                }
            }

            stopwatch.Stop();
            _logger.LogDebug("通知處理完成，耗時 {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (SqlException ex) when (ex.Number == -2 || ex.Number == 2)
        {
            // SQL 超時錯誤
            stopwatch.Stop();
            _logger.LogWarning(ex, "查詢超時，跳過本次通知處理。耗時 {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (SqlException ex) when (ex.Number == 53 || ex.Number == 121 || ex.Number == 10054 || ex.Number == 10060)
        {
            // 連線錯誤：53=Network path not found, 121=Semaphore timeout, 10054=Connection reset, 10060=Timeout expired
            stopwatch.Stop();
            _logger.LogWarning(ex, "資料庫連線錯誤，跳過本次通知處理。耗時 {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "處理通知佇列時發生未預期的錯誤。耗時 {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
    }
}

