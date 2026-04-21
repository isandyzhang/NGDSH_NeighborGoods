using System;
using System.Collections.Generic;

namespace NeighborGoods.Api.Shared.Persistence.LegacyEntities;

public partial class Conversation
{
    public Guid Id { get; set; }

    public string Participant1Id { get; set; } = null!;

    public string Participant2Id { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? Participant1LastReadAt { get; set; }

    public DateTime? Participant2LastReadAt { get; set; }

    public Guid ListingId { get; set; }

    public virtual global::NeighborGoods.Api.Features.Listing.Listing Listing { get; set; } = null!;

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual ICollection<PurchaseRequest> PurchaseRequests { get; set; } = new List<PurchaseRequest>();

    public virtual AspNetUser Participant1 { get; set; } = null!;

    public virtual AspNetUser Participant2 { get; set; } = null!;
}
