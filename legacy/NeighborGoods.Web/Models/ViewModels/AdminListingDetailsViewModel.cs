using NeighborGoods.Web.Models.Enums;

namespace NeighborGoods.Web.Models.ViewModels;

public class AdminListingDetailsViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsFree { get; set; }
    public bool IsCharity { get; set; }
    public bool IsTradeable { get; set; }
    public ListingStatus Status { get; set; }
    public ListingCategory Category { get; set; }
    public ListingCondition Condition { get; set; }
    public ListingPickupLocation PickupLocation { get; set; }
    public string SellerId { get; set; } = string.Empty;
    public string SellerDisplayName { get; set; } = string.Empty;
    public string? BuyerId { get; set; }
    public string? BuyerDisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public List<AdminConversationItemViewModel> Conversations { get; set; } = new();
}

public class AdminConversationItemViewModel
{
    public Guid ConversationId { get; set; }
    public string Participant1Id { get; set; } = string.Empty;
    public string Participant1DisplayName { get; set; } = string.Empty;
    public string Participant2Id { get; set; } = string.Empty;
    public string Participant2DisplayName { get; set; } = string.Empty;
    public List<AdminMessageItemViewModel> Messages { get; set; } = new();
}

public class AdminMessageItemViewModel
{
    public string SenderId { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsSeller { get; set; }
}

