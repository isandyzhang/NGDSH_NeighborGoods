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

    private async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var now = DateTime.UtcNow;
        var thresholdTime = now.AddMinutes(-UnreadDelayMinutes);

        var users = await dbContext.AspNetUsers
            .Where(x => x.EmailNotificationEnabled && x.EmailConfirmed && x.Email != null && x.Email != string.Empty)
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.EmailNotificationLastSentAt
            })
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            return;
        }

        var userIds = users.Select(x => x.Id).ToHashSet();
        var userEmailDict = users.ToDictionary(x => x.Id, x => x.Email!);
        var userEmailLastSentAtDict = users.ToDictionary(x => x.Id, x => x.EmailNotificationLastSentAt);

        var conversations = await dbContext.Conversations
            .Where(x => userIds.Contains(x.Participant1Id) || userIds.Contains(x.Participant2Id))
            .Select(x => new
            {
                x.Id,
                x.Participant1Id,
                x.Participant2Id,
                x.Participant1LastReadAt,
                x.Participant2LastReadAt
            })
            .ToListAsync(cancellationToken);

        if (conversations.Count == 0)
        {
            return;
        }

        var conversationDict = conversations.ToDictionary(x => x.Id);
        var conversationIds = conversations.Select(x => x.Id).ToList();

        var candidateMessages = await dbContext.Messages
            .Where(x => conversationIds.Contains(x.ConversationId) && x.CreatedAt <= thresholdTime)
            .Select(x => new
            {
                x.ConversationId,
                x.SenderId,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        if (candidateMessages.Count == 0)
        {
            return;
        }

        var usersToNotify = new Dictionary<string, Guid>();
        foreach (var message in candidateMessages)
        {
            if (!conversationDict.TryGetValue(message.ConversationId, out var conversation))
            {
                continue;
            }

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

            if (string.IsNullOrWhiteSpace(targetUserId))
            {
                continue;
            }

            if (lastReadAt.HasValue && message.CreatedAt <= lastReadAt.Value)
            {
                continue;
            }

            if (!userEmailLastSentAtDict.TryGetValue(targetUserId, out var emailLastSentAt) ||
                (emailLastSentAt.HasValue && message.CreatedAt <= emailLastSentAt.Value))
            {
                continue;
            }

            if (!usersToNotify.ContainsKey(targetUserId))
            {
                usersToNotify[targetUserId] = message.ConversationId;
            }
        }

        if (usersToNotify.Count == 0)
        {
            return;
        }

        var successCount = 0;
        foreach (var (userId, conversationId) in usersToNotify)
        {
            if (!userEmailDict.TryGetValue(userId, out var userEmail))
            {
                continue;
            }

            try
            {
                var chatUrl = $"{_webBaseUrl}/messages/{conversationId}";
                await emailSender.SendAsync(
                    userEmail,
                    "你有未讀訊息",
                    $"你有尚未讀取的新訊息。\n\n請點擊此連結查看：{chatUrl}",
                    cancellationToken);

                var user = await dbContext.AspNetUsers.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
                if (user != null)
                {
                    user.EmailNotificationLastSentAt = now;
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send unread message email notification. UserId={UserId}", userId);
            }
        }

        if (successCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
