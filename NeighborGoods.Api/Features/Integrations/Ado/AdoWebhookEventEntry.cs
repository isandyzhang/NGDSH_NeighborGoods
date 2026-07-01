namespace NeighborGoods.Api.Features.Integrations.Ado;

public sealed record AdoWebhookEventEntry(
    Guid Id,
    DateTime ReceivedAt,
    string RawBody,
    string? EventType = null,
    int? WorkItemId = null,
    string? WorkItemTitle = null,
    string? ProjectName = null,
    string? WorkItemUrl = null,
    string? SummaryPreview = null,
    string? NormalizedSummary = null,
    string FieldResolveStatus = "pending",
    string? NormalizeError = null,
    string LineNotifyStatus = "pending");
