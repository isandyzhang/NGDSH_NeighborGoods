using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeighborGoods.Workers.Line;
using NeighborGoods.Workers.Messaging;
using NeighborGoods.Workers.PurchaseRequests;

namespace NeighborGoods.Functions;

public sealed class ScheduledTriggers(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledTriggers> logger)
{
    [Function(nameof(UnreadMessageEmailTimer))]
    public async Task UnreadMessageEmailTimer(
        [TimerTrigger("0 */1 * * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<UnreadMessageEmailNotificationJob>()
                .RunOnceAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Function} failed.", nameof(UnreadMessageEmailTimer));
        }
    }

    [Function(nameof(LinePreferencePushTimer))]
    public async Task LinePreferencePushTimer(
        [TimerTrigger("0 */5 * * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<LinePreferencePushJob>()
                .RunOnceAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Function} failed.", nameof(LinePreferencePushTimer));
        }
    }

    [Function(nameof(PurchaseRequestExpirationTimer))]
    public async Task PurchaseRequestExpirationTimer(
        [TimerTrigger("0 */10 * * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var ops = scope.ServiceProvider.GetRequiredService<IPurchaseRequestScheduledOperations>();
            var reminded = await ops.SendSellerReminderAsync(cancellationToken);
            var expired = await ops.ExpirePendingAsync(cancellationToken);
            if (reminded > 0 || expired > 0)
            {
                logger.LogInformation(
                    "PurchaseRequest timer: reminders={Reminded}, expired={Expired}",
                    reminded,
                    expired);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Function} failed.", nameof(PurchaseRequestExpirationTimer));
        }
    }

    [Function(nameof(QuickResponderBadgeTimer))]
    public async Task QuickResponderBadgeTimer(
        [TimerTrigger("0 0 2 * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<QuickResponderEvaluationService>()
                .EvaluateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Function} failed.", nameof(QuickResponderBadgeTimer));
        }
    }
}
