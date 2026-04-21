using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NeighborGoods.Api.Shared.Notifications;

public sealed class LineMessagingQuotaService(
    HttpClient httpClient,
    IOptions<LineMessagingOptions> options,
    LinePushQuotaTracker quotaTracker,
    ILogger<LineMessagingQuotaService> logger)
{
    private readonly LineMessagingOptions _options = options.Value;

    public async Task<LineMessagingQuotaStatus> GetCurrentQuotaAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ChannelAccessToken))
        {
            return BuildEstimatedStatus("未設定 ChannelAccessToken，使用本機估算值。");
        }

        try
        {
            var quota = await GetQuotaValueAsync(cancellationToken);
            var usage = await GetUsageValueAsync(cancellationToken);
            if (quota is null || usage is null)
            {
                return BuildEstimatedStatus("LINE quota API 未回傳完整資料，使用本機估算值。");
            }

            if (quota.Value <= 0)
            {
                return new LineMessagingQuotaStatus(
                    IsEstimated: false,
                    PlanType: "none",
                    MonthlyQuota: null,
                    UsedCount: usage.Value,
                    RemainingCount: null,
                    UsagePercent: null,
                    Note: "目前方案為無上限或不受配額限制。");
            }

            var remaining = Math.Max(quota.Value - usage.Value, 0);
            var usagePercent = (int)Math.Round((usage.Value * 100.0) / quota.Value, MidpointRounding.AwayFromZero);
            return new LineMessagingQuotaStatus(
                IsEstimated: false,
                PlanType: "limited",
                MonthlyQuota: quota.Value,
                UsedCount: usage.Value,
                RemainingCount: remaining,
                UsagePercent: usagePercent,
                Note: "來源：LINE Messaging API quota。");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "讀取 LINE quota API 失敗，改用本機估算值。");
            return BuildEstimatedStatus("LINE quota API 讀取失敗，使用本機估算值。");
        }
    }

    private async Task<int?> GetQuotaValueAsync(CancellationToken cancellationToken)
    {
        using var request = CreateRequest("/message/quota");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
        using var response = await httpClient.SendAsync(request, timeoutCts.Token);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: timeoutCts.Token);

        var root = doc.RootElement;
        if (root.TryGetProperty("type", out var typeElement))
        {
            var type = typeElement.GetString();
            if (string.Equals(type, "none", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
        }

        if (root.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Number)
        {
            return valueElement.GetInt32();
        }

        return null;
    }

    private async Task<int?> GetUsageValueAsync(CancellationToken cancellationToken)
    {
        using var request = CreateRequest("/message/quota/consumption");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
        using var response = await httpClient.SendAsync(request, timeoutCts.Token);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: timeoutCts.Token);
        if (doc.RootElement.TryGetProperty("totalUsage", out var totalUsage)
            && totalUsage.ValueKind == JsonValueKind.Number)
        {
            return totalUsage.GetInt32();
        }

        return null;
    }

    private HttpRequestMessage CreateRequest(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl.TrimEnd('/')}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ChannelAccessToken);
        return request;
    }

    private LineMessagingQuotaStatus BuildEstimatedStatus(string note)
    {
        var quota = Math.Max(1, _options.MonthlyPushQuota);
        var used = quotaTracker.GetCurrentMonthPushCount();
        var remaining = Math.Max(quota - used, 0);
        var percent = (int)Math.Round((used * 100.0) / quota, MidpointRounding.AwayFromZero);
        return new LineMessagingQuotaStatus(
            IsEstimated: true,
            PlanType: "limited",
            MonthlyQuota: quota,
            UsedCount: used,
            RemainingCount: remaining,
            UsagePercent: percent,
            Note: note);
    }
}

public sealed record LineMessagingQuotaStatus(
    bool IsEstimated,
    string PlanType,
    int? MonthlyQuota,
    int UsedCount,
    int? RemainingCount,
    int? UsagePercent,
    string Note);
