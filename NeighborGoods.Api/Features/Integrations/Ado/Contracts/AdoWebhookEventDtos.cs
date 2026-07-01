namespace NeighborGoods.Api.Features.Integrations.Ado.Contracts;

public sealed record AdoWebhookEventListItemDto(
    Guid Id,
    DateTime ReceivedAt,
    int BodyLength,
    string RawBodyPreview,
    string? EventType,
    int? WorkItemId,
    string? WorkItemTitle,
    string? SummaryPreview,
    string FieldResolveStatus);

public sealed record AdoWebhookEventDetailDto(
    Guid Id,
    DateTime ReceivedAt,
    int BodyLength,
    string RawBody,
    string? EventType,
    int? WorkItemId,
    string? WorkItemTitle,
    string? ProjectName,
    string? WorkItemUrl,
    string? SummaryPreview,
    string? NormalizedSummary,
    string FieldResolveStatus,
    string? NormalizeError);

public sealed record AdoWebhookEventListResponse(
    IReadOnlyList<AdoWebhookEventListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
