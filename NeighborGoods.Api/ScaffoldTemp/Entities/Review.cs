using System;
using System.Collections.Generic;

namespace NeighborGoods.Api.Shared.Persistence.LegacyEntities;

public partial class Review
{
    public Guid Id { get; set; }

    public Guid ListingId { get; set; }

    public string SellerId { get; set; } = null!;

    public string BuyerId { get; set; } = null!;

    /// <summary>對應的購買請求；雙向評價時以 (PurchaseRequestId, ReviewerId) 唯一。</summary>
    public Guid? PurchaseRequestId { get; set; }

    /// <summary>撰寫此評價的使用者 Id（買家評賣家或賣家評買家）。</summary>
    public string? ReviewerId { get; set; }

    public int Rating { get; set; }

    public string? Content { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual AspNetUser Buyer { get; set; } = null!;

    public virtual global::NeighborGoods.Api.Features.Listing.Listing Listing { get; set; } = null!;

    public virtual AspNetUser Seller { get; set; } = null!;

    public virtual PurchaseRequest? PurchaseRequest { get; set; }
}
