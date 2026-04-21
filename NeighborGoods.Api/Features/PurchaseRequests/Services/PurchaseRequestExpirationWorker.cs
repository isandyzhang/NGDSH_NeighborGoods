namespace NeighborGoods.Api.Features.PurchaseRequests.Services;

public sealed class PurchaseRequestExpirationWorker(
    IServiceProvider serviceProvider,
    ILogger<PurchaseRequestExpirationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan LoopInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<PurchaseRequestService>();

                var remindedCount = await service.SendSellerReminderAsync(stoppingToken);
                var expiredCount = await service.ExpirePendingAsync(stoppingToken);
                if (remindedCount > 0 || expiredCount > 0)
                {
                    logger.LogInformation(
                        "PurchaseRequest worker processed reminders={RemindedCount}, expired={ExpiredCount}",
                        remindedCount,
                        expiredCount);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PurchaseRequestExpirationWorker failed.");
            }

            await Task.Delay(LoopInterval, stoppingToken);
        }
    }
}
