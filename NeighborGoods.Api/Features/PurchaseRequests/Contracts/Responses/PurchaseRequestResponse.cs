using NeighborGoods.Api.Features.PurchaseRequests;

namespace NeighborGoods.Api.Features.PurchaseRequests.Contracts.Responses;

public sealed class PurchaseRequestResponse
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public Guid ConversationId { get; set; }
    public string BuyerId { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public PurchaseRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpireAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? ResponseReason { get; set; }
    public int RemainingSeconds { get; set; }
}
