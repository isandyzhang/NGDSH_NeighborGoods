namespace NeighborGoods.Api.Features.Messaging.Contracts.Responses;

public sealed class MessageItemDto
{
    public Guid Id { get; init; }

    public string SenderId { get; init; } = string.Empty;

    public string SenderDisplayName { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}
