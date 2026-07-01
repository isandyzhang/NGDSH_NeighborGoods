namespace NeighborGoods.Api.Features.Integrations.Ado;

public sealed record AdoWebhookNormalizationResult(
    string? EventType,
    int? WorkItemId,
    string? WorkItemTitle,
    string? ProjectName,
    string? WorkItemUrl,
    string? SummaryPreview,
    string? NormalizedSummary,
    string FieldResolveStatus,
    string? NormalizeError);
