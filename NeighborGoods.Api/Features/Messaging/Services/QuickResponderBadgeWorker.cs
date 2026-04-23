namespace NeighborGoods.Api.Features.Messaging.Services;

public sealed class QuickResponderBadgeWorker(
    IServiceProvider serviceProvider,
    ILogger<QuickResponderBadgeWorker> logger) : BackgroundService
{
    private const int DailyRunHourUtc = 2;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRun(DateTime.UtcNow);
            logger.LogInformation(
                "QuickResponderBadgeWorker scheduled next run in {DelayMinutes} minutes.",
                Math.Round(delay.TotalMinutes, 1));

            await Task.Delay(delay, stoppingToken);
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var evaluationService = scope.ServiceProvider.GetRequiredService<QuickResponderEvaluationService>();
                await evaluationService.EvaluateAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "QuickResponderBadgeWorker failed.");
            }
        }
    }

    private static TimeSpan GetDelayUntilNextRun(DateTime nowUtc)
    {
        var nextRunUtc = new DateTime(
            nowUtc.Year,
            nowUtc.Month,
            nowUtc.Day,
            DailyRunHourUtc,
            0,
            0,
            DateTimeKind.Utc);

        if (nowUtc >= nextRunUtc)
        {
            nextRunUtc = nextRunUtc.AddDays(1);
        }

        return nextRunUtc - nowUtc;
    }
}
