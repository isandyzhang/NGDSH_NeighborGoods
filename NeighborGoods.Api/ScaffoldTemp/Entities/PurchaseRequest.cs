using System;

namespace NeighborGoods.Api.Shared.Persistence.LegacyEntities;

public partial class PurchaseRequest
{
    public Guid Id { get; set; }

    public Guid ListingId { get; set; }

    public Guid ConversationId { get; set; }

    public string BuyerId { get; set; } = null!;

    public string SellerId { get; set; } = null!;

    public int Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpireAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public string? ResponseReason { get; set; }

    public DateTime? SellerReminderSentAt { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual global::NeighborGoods.Api.Features.Listing.Listing Listing { get; set; } = null!;

    public virtual AspNetUser Buyer { get; set; } = null!;

    public virtual AspNetUser Seller { get; set; } = null!;
}
