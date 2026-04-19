namespace NeighborGoods.Api.Features.Messaging.Contracts.Requests;

public sealed class EnsureConversationRequest
{
    public Guid ListingId { get; set; }

    public string OtherUserId { get; set; } = string.Empty;
}
