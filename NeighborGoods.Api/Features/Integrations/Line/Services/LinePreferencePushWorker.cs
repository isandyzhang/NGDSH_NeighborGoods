using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeighborGoods.Api.Shared.Notifications;
using NeighborGoods.Api.Shared.Persistence;

namespace NeighborGoods.Api.Features.Integrations.Line.Services;

public sealed class LinePreferencePushWorker(
    IServiceProvider serviceProvider,
    IOptions<LineMessagingOptions> options,
    ILogger<LinePreferencePushWorker> logger) : BackgroundService
{
    private readonly LineMessagingOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "LinePreferencePushWorker failed.");
            }

            var interval = Math.Max(5, _options.PreferencePushIntervalMinutes);
            await Task.Delay(TimeSpan.FromMinutes(interval), stoppingToken);
        }
    }

    private async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        if (!_options.PreferencePushEnabled)
        {
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var lineMessageSender = scope.ServiceProvider.GetRequiredService<ILineMessageSender>();
        var policyService = scope.ServiceProvider.GetRequiredService<LinePushPolicyService>();
        var flexBuilder = scope.ServiceProvider.GetRequiredService<LineFlexMessageBuilder>();

        var now = DateTime.UtcNow;
        var batchSize = Math.Max(1, _options.PreferencePushBatchSize);
        var users = await dbContext.AspNetUsers
            .Where(x =>
                x.LineMessagingApiUserId != null &&
                x.LineMessagingApiAuthorizedAt != null &&
                x.LineNotificationPreference != 0)
            .OrderBy(x => x.LineNotificationLastSentAt ?? DateTime.MinValue)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var sentCount = 0;
        foreach (var user in users)
        {
            var preferenceFlags = (LineNotificationPreferenceFlags)user.LineNotificationPreference;
            var category = ResolveCategory(preferenceFlags);
            if (!category.HasValue)
            {
                continue;
            }

            if (!policyService.CanSendPreferencePush(user, category.Value, now))
            {
                continue;
            }

            var card = category.Value switch
            {
                LineNotificationPreferenceFlags.PriceDrop => flexBuilder.BuildNoticeCard(
                    "你關注的商品有價格更新",
                    "系統偵測到你可能有興趣的降價商品，快來看看。"),
                LineNotificationPreferenceFlags.MessageDigest => flexBuilder.BuildNoticeCard(
                    "鄰里最新互動摘要",
                    "有新的社區互動與訊息，歡迎回來看看。"),
                _ => flexBuilder.BuildNoticeCard(
                    "有新商品上架",
                    "你關注的類型可能有新刊登，立即查看。")
            };

            await lineMessageSender.PushFlexAsync(user.LineMessagingApiUserId!, card.AltText, card.Contents, cancellationToken);
            user.LineNotificationLastSentAt = now;
            sentCount += 1;
        }

        if (sentCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "LinePreferencePushWorker processed users={Users}, sent={Sent}, usagePercent={UsagePercent}",
            users.Count,
            sentCount,
            policyService.GetCurrentUsagePercent());
    }

    private static LineNotificationPreferenceFlags? ResolveCategory(LineNotificationPreferenceFlags preferences)
    {
        if (preferences.HasFlag(LineNotificationPreferenceFlags.NewListings))
        {
            return LineNotificationPreferenceFlags.NewListings;
        }

        if (preferences.HasFlag(LineNotificationPreferenceFlags.PriceDrop))
        {
            return LineNotificationPreferenceFlags.PriceDrop;
        }

        if (preferences.HasFlag(LineNotificationPreferenceFlags.MessageDigest))
        {
            return LineNotificationPreferenceFlags.MessageDigest;
        }

        return null;
    }
}
