namespace NeighborGoods.Api.Features.Integrations.Ado;

public sealed record AdoWebhookEventEntry(
    Guid Id,
    DateTime ReceivedAt,
    string RawBody);
