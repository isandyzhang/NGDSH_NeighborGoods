using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeighborGoods.Api.Shared.Notifications;
using NeighborGoods.Api.Shared.Persistence;

namespace NeighborGoods.Api.Features.Messaging.Services;

public sealed class UnreadMessageEmailNotificationWorker(
    IServiceProvider serviceProvider,
    IOptions<LineMessagingOptions> lineMessagingOptions,
    ILogger<UnreadMessageEmailNotificationWorker> logger) : BackgroundService
{
    private const int CheckIntervalMinutes = 1;
    private const int UnreadDelayMinutes = 5;
    private const string DefaultWebBaseUrl = "https://neighborgoodstw.com";

    private readonly string _webBaseUrl = string.IsNullOrWhiteSpace(lineMessagingOptions.Value.WebBaseUrl)
        ? DefaultWebBaseUrl
        : lineMessagingOptions.Value.WebBaseUrl.TrimEnd('/');

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "UnreadMessageEmailNotificationWorker started. DelayMinutes={DelayMinutes}, IntervalMinutes={IntervalMinutes}",
            UnreadDelayMinutes,
            CheckIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "UnreadMessageEmailNotificationWorker failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(CheckIntervalMinutes), stoppingToken);
        }
    }

    internal async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var now = DateTime.UtcNow;
        var thresholdTime = now.AddMinutes(-UnreadDelayMinutes);

        var participant1UnreadMessages =
            from message in dbContext.Messages
            join conversation in dbContext.Conversations on message.ConversationId equals conversation.Id
            join user in dbContext.AspNetUsers on conversation.Participant1Id equals user.Id
            where conversation.Participant1Id != message.SenderId &&
                  message.CreatedAt <= thresholdTime &&
                  (!conversation.Participant1LastReadAt.HasValue ||
                   message.CreatedAt > conversation.Participant1LastReadAt.Value) &&
                  user.EmailNotificationEnabled &&
                  user.EmailConfirmed &&
                  user.Email != null &&
                  user.Email != string.Empty &&
                  (!user.EmailNotificationLastSentAt.HasValue ||
                   message.CreatedAt > user.EmailNotificationLastSentAt.Value)
            select new
            {
                UserId = user.Id,
                Email = user.Email,
                ConversationId = conversation.Id,
                message.CreatedAt
            };

        var participant2UnreadMessages =
            from message in dbContext.Messages
            join conversation in dbContext.Conversations on message.ConversationId equals conversation.Id
            join user in dbContext.AspNetUsers on conversation.Participant2Id equals user.Id
            where conversation.Participant2Id != message.SenderId &&
                  message.CreatedAt <= thresholdTime &&
                  (!conversation.Participant2LastReadAt.HasValue ||
                   message.CreatedAt > conversation.Participant2LastReadAt.Value) &&
                  user.EmailNotificationEnabled &&
                  user.EmailConfirmed &&
                  user.Email != null &&
                  user.Email != string.Empty &&
                  (!user.EmailNotificationLastSentAt.HasValue ||
                   message.CreatedAt > user.EmailNotificationLastSentAt.Value)
            select new
            {
                UserId = user.Id,
                Email = user.Email,
                ConversationId = conversation.Id,
                message.CreatedAt
            };

        var usersToNotify = await participant1UnreadMessages
            .Concat(participant2UnreadMessages)
            .GroupBy(x => new { x.UserId, x.Email })
            .Select(x => new
            {
                x.Key.UserId,
                Email = x.Key.Email!,
                ConversationId = x
                    .OrderByDescending(message => message.CreatedAt)
                    .Select(message => message.ConversationId)
                    .First()
            })
            .ToListAsync(cancellationToken);

        if (usersToNotify.Count == 0)
        {
            return;
        }

        var successCount = 0;
        foreach (var notification in usersToNotify)
        {
            try
            {
                var chatUrl = $"{_webBaseUrl}/messages/{notification.ConversationId}";
                await emailSender.SendAsync(
                    notification.Email,
                    "你有未讀訊息",
                    $"你有尚未讀取的新訊息。\n\n請點擊此連結查看：{chatUrl}",
                    cancellationToken);

                var user = await dbContext.AspNetUsers
                    .FirstOrDefaultAsync(x => x.Id == notification.UserId, cancellationToken);
                if (user != null)
                {
                    user.EmailNotificationLastSentAt = now;
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to send unread message email notification. UserId={UserId}",
                    notification.UserId);
            }
        }

        if (successCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
