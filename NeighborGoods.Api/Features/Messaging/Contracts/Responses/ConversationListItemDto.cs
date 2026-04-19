namespace NeighborGoods.Api.Features.Messaging.Contracts.Responses;

public sealed class ConversationListItemDto
{
    public Guid ConversationId { get; init; }

    public Guid ListingId { get; init; }

    public string ListingTitle { get; init; } = string.Empty;

    public string OtherUserId { get; init; } = string.Empty;

    public string OtherDisplayName { get; init; } = string.Empty;

    public string? LastMessagePreview { get; init; }

    public DateTime? LastMessageAt { get; init; }

    public int UnreadCount { get; init; }
}
