using System;
using System.Collections.Generic;

namespace NeighborGoods.Api.Shared.Persistence.LegacyEntities;

public partial class ListingTopSubmission
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public Guid? ListingId { get; set; }

    public string PhotoBlobName { get; set; } = null!;

    public string FeedbackTitle { get; set; } = null!;

    public string FeedbackDetail { get; set; } = null!;

    public bool AllowPromotion { get; set; }

    public int Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewedByAdminId { get; set; }

    public int GrantedCredits { get; set; }

    public virtual global::NeighborGoods.Api.Features.Listing.Listing? Listing { get; set; }

    public virtual AspNetUser? ReviewedByAdmin { get; set; }

    public virtual AspNetUser User { get; set; } = null!;
}
