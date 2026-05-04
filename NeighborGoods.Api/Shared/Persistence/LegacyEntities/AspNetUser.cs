using System;
using System.Collections.Generic;

namespace NeighborGoods.Api.Shared.Persistence.LegacyEntities;

public partial class AspNetUser
{
    public string Id { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? LineUserId { get; set; }

    public int Role { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? UserName { get; set; }

    public string? NormalizedUserName { get; set; }

    public string? Email { get; set; }

    public string? NormalizedEmail { get; set; }

    public bool EmailConfirmed { get; set; }

    public string? PasswordHash { get; set; }

    public string? SecurityStamp { get; set; }

    public string? ConcurrencyStamp { get; set; }

    public string? PhoneNumber { get; set; }

    public bool PhoneNumberConfirmed { get; set; }

    public bool TwoFactorEnabled { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public bool LockoutEnabled { get; set; }

    public int AccessFailedCount { get; set; }

    public DateTime? LineMessagingApiAuthorizedAt { get; set; }

    public string? LineMessagingApiUserId { get; set; }

    public int LineNotificationPreference { get; set; }

    public DateTime? LineNotificationLastSentAt { get; set; }

    public bool EmailNotificationEnabled { get; set; }

    public DateTime? EmailNotificationLastSentAt { get; set; }

    public int TopPinCredits { get; set; }

    public bool IsQuickResponder { get; set; }

    public DateTime? QuickResponderEvaluatedAt { get; set; }

    public int? QuickResponderP75Minutes { get; set; }

    public virtual ICollection<AdminMessage> AdminMessages { get; set; } = new List<AdminMessage>();

    public virtual ICollection<AspNetUserClaim> AspNetUserClaims { get; set; } = new List<AspNetUserClaim>();

    public virtual ICollection<AspNetUserLogin> AspNetUserLogins { get; set; } = new List<AspNetUserLogin>();

    public virtual ICollection<AspNetUserToken> AspNetUserTokens { get; set; } = new List<AspNetUserToken>();

    public virtual ICollection<Conversation> ConversationParticipant1s { get; set; } = new List<Conversation>();

    public virtual ICollection<Conversation> ConversationParticipant2s { get; set; } = new List<Conversation>();

    public virtual ICollection<LineBindingPending> LineBindingPendings { get; set; } = new List<LineBindingPending>();

    public virtual ICollection<global::NeighborGoods.Api.Features.Listing.Listing> ListingBuyers { get; set; } = new List<global::NeighborGoods.Api.Features.Listing.Listing>();

    public virtual ICollection<global::NeighborGoods.Api.Features.Listing.Listing> ListingSellers { get; set; } = new List<global::NeighborGoods.Api.Features.Listing.Listing>();

    public virtual ICollection<ListingTopSubmission> ListingTopSubmissionReviewedByAdmins { get; set; } = new List<ListingTopSubmission>();

    public virtual ICollection<ListingTopSubmission> ListingTopSubmissionUsers { get; set; } = new List<ListingTopSubmission>();

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual ICollection<PurchaseRequest> PurchaseRequestBuyers { get; set; } = new List<PurchaseRequest>();

    public virtual ICollection<PurchaseRequest> PurchaseRequestSellers { get; set; } = new List<PurchaseRequest>();

    public virtual ICollection<Review> ReviewBuyers { get; set; } = new List<Review>();

    public virtual ICollection<Review> ReviewSellers { get; set; } = new List<Review>();

    public virtual ICollection<AspNetRole> Roles { get; set; } = new List<AspNetRole>();
}
