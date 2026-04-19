namespace NeighborGoods.Api.Features.Messaging.Contracts.Responses;

public sealed class MessagesPageResponse
{
    public IReadOnlyList<MessageItemDto> Items { get; init; } = Array.Empty<MessageItemDto>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages { get; init; }
}
