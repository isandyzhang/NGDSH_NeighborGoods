using Microsoft.Extensions.Caching.Memory;

namespace NeighborGoods.Api.Shared.Notifications;

public sealed class LinePushQuotaTracker(IMemoryCache memoryCache)
{
    private const string KeyPrefix = "line-push-count:";

    public int GetCurrentMonthPushCount()
    {
        return memoryCache.TryGetValue(GetKey(), out int count) ? count : 0;
    }

    public void Increment()
    {
        var key = GetKey();
        var expiresAt = new DateTimeOffset(
            DateTime.UtcNow.Year,
            DateTime.UtcNow.Month,
            1,
            0,
            0,
            0,
            TimeSpan.Zero).AddMonths(1);

        var current = memoryCache.TryGetValue(key, out int count) ? count : 0;
        memoryCache.Set(key, current + 1, expiresAt);
    }

    private static string GetKey() => $"{KeyPrefix}{DateTime.UtcNow:yyyyMM}";
}
