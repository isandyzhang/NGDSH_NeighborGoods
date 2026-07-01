namespace NeighborGoods.Api.Features.Integrations.Ado.Contracts;

public sealed record AdoWebhookEventListItemDto(
    Guid Id,
    DateTime ReceivedAt,
    int BodyLength,
    string RawBodyPreview);

public sealed record AdoWebhookEventDetailDto(
    Guid Id,
    DateTime ReceivedAt,
    int BodyLength,
    string RawBody);

public sealed record AdoWebhookEventListResponse(
    IReadOnlyList<AdoWebhookEventListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
