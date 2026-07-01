using Microsoft.Extensions.Options;
using NeighborGoods.Notifications;

namespace NeighborGoods.Api.Features.Integrations.Ado;

public sealed class AdoWebhookLineNotifier(
    ILineMessageSender lineMessageSender,
    IOptions<AdoWebhookOptions> options,
    ILogger<AdoWebhookLineNotifier> logger)
{
    private readonly AdoWebhookOptions _options = options.Value;

    public async Task<string> TryNotifyAsync(
        AdoWebhookNormalizationResult result,
        CancellationToken cancellationToken = default)
    {
        if (!_options.LineNotifyEnabled)
        {
            return "skipped";
        }

        if (string.IsNullOrWhiteSpace(_options.LineGroupId))
        {
            return "skipped";
        }

        if (!string.Equals(result.EventType, "workitem.updated", StringComparison.OrdinalIgnoreCase))
        {
            return "skipped";
        }

        if (string.IsNullOrWhiteSpace(result.SummaryPreview))
        {
            return "skipped";
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(result.WorkItemUrl))
            {
                await lineMessageSender.SendLinkAsync(
                    _options.LineGroupId,
                    result.SummaryPreview,
                    result.WorkItemUrl,
                    "開啟 Work Item",
                    cancellationToken);
            }
            else
            {
                await lineMessageSender.SendTextAsync(
                    _options.LineGroupId,
                    result.SummaryPreview,
                    cancellationToken);
            }

            return "sent";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ADO webhook LINE notify failed for event type {EventType}", result.EventType);
            return "failed";
        }
    }
}
