using Microsoft.Extensions.Options;

namespace NeighborGoods.Api.Features.Integrations.Ado;

public sealed class AdoWebhookProcessor(
    AdoFieldsClient fieldsClient,
    AdoWebhookNormalizer normalizer,
    AdoWebhookMemoryStore store,
    IOptions<AdoWebhookOptions> options,
    ILogger<AdoWebhookProcessor> logger)
{
    private readonly AdoWebhookOptions _options = options.Value;

    public async Task ProcessAsync(Guid eventId, string rawBody, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, string> fieldDisplayNames;
        string? fieldsError = null;

        try
        {
            fieldDisplayNames = await fieldsClient.GetFieldDisplayNamesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            fieldsError = ex.Message;
            fieldDisplayNames = new Dictionary<string, string>();
            logger.LogWarning(ex, "ADO fields lookup failed for webhook event {EventId}", eventId);
        }

        var result = normalizer.Normalize(rawBody, fieldDisplayNames);
        if (fieldsError is not null)
        {
            result = result with
            {
                FieldResolveStatus = result.FieldResolveStatus is "success" or "partial" ? "failed" : result.FieldResolveStatus,
                NormalizeError = fieldsError,
            };
        }

        var updated = store.TryUpdate(eventId, entry => entry with
        {
            EventType = result.EventType,
            WorkItemId = result.WorkItemId,
            WorkItemTitle = result.WorkItemTitle,
            ProjectName = result.ProjectName,
            WorkItemUrl = result.WorkItemUrl,
            SummaryPreview = result.SummaryPreview,
            NormalizedSummary = result.NormalizedSummary,
            FieldResolveStatus = HasPatConfigured() ? result.FieldResolveStatus : PreferSkippedStatus(result.FieldResolveStatus),
            NormalizeError = result.NormalizeError,
        });

        if (!updated)
        {
            logger.LogWarning("ADO webhook event {EventId} not found for normalization update", eventId);
        }
    }

    private bool HasPatConfigured() =>
        !string.IsNullOrWhiteSpace(_options.PersonalAccessToken)
        && !string.IsNullOrWhiteSpace(_options.OrganizationUrl);

    private static string PreferSkippedStatus(string status) =>
        status is "success" or "partial" ? "skipped" : status;
}
