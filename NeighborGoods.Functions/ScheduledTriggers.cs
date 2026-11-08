using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeighborGoods.Workers.Line;
using NeighborGoods.Workers.Listings;
using NeighborGoods.Workers.Messaging;
using NeighborGoods.Workers.PurchaseRequests;

namespace NeighborGoods.Functions;

public sealed class ScheduledTriggers(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledTriggers> logger)
{
    [Function(nameof(UnreadMessageEmailTimer))]
    public Task UnreadMessageEmailTimer(
        [TimerTrigger("0 */1 * * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken) =>
        RunAsync(
            nameof(UnreadMessageEmailTimer),
            async (sp, ct) =>
                await sp.GetRequiredService<UnreadMessageEmailNotificationJob>().RunOnceAsync(ct),
            cancellationToken);

    [Function(nameof(UnreadMessageLineTimer))]
    public Task UnreadMessageLineTimer(
        [TimerTrigger("0 */1 * * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken) =>
        RunAsync(
            nameof(UnreadMessageLineTimer),
            async (sp, ct) =>
                await sp.GetRequiredService<UnreadMessageLineNotificationJob>().RunOnceAsync(ct),
            cancellationToken);

    [Function(nameof(LinePreferencePushTimer))]
    public Task LinePreferencePushTimer(
        [TimerTrigger("0 */5 * * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken) =>
        RunAsync(
            nameof(LinePreferencePushTimer),
            async (sp, ct) =>
                await sp.GetRequiredService<LinePreferencePushJob>().RunOnceAsync(ct),
            cancellationToken);

    [Function(nameof(PurchaseRequestExpirationTimer))]
    public Task PurchaseRequestExpirationTimer(
        [TimerTrigger("0 */10 * * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken) =>
        RunAsync(
            nameof(PurchaseRequestExpirationTimer),
            async (sp, ct) =>
            {
                var ops = sp.GetRequiredService<IPurchaseRequestScheduledOperations>();
                var reminded = await ops.SendSellerReminderAsync(ct);
                var expired = await ops.ExpirePendingAsync(ct);
                if (reminded > 0 || expired > 0)
                {
                    logger.LogInformation(
                        "PurchaseRequest timer: reminders={Reminded}, expired={Expired}",
                        reminded,
                        expired);
                }
            },
            cancellationToken);

    [Function(nameof(ListingExpiryTimer))]
    public Task ListingExpiryTimer(
        [TimerTrigger("0 0 */1 * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken) =>
        RunAsync(
            nameof(ListingExpiryTimer),
            async (sp, ct) =>
            {
                var processed = await sp.GetRequiredService<ListingExpiryJob>().RunOnceAsync(ct);
                if (processed > 0)
                {
                    logger.LogInformation("ListingExpiry timer: processed={Processed}", processed);
                }
            },
            cancellationToken);

    [Function(nameof(QuickResponderBadgeTimer))]
    public Task QuickResponderBadgeTimer(
        [TimerTrigger("0 0 2 * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken) =>
        RunAsync(
            nameof(QuickResponderBadgeTimer),
            async (sp, ct) =>
                await sp.GetRequiredService<QuickResponderEvaluationService>().EvaluateAsync(ct),
            cancellationToken);

    private async Task RunAsync(
        string functionName,
        Func<IServiceProvider, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await action(scope.ServiceProvider, cancellationToken);
        }
        catch (Exception ex)
        {
            // 必須再拋出，Azure 才會記成失敗；先前只 LogError 會被標成 Succeeded。
            logger.LogError(ex, "{Function} failed.", functionName);
            throw;
        }
    }
}
