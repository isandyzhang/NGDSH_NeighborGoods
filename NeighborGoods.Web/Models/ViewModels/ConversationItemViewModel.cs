namespace NeighborGoods.Web.Models.ViewModels;

public class ConversationItemViewModel
{
    public Guid ConversationId { get; set; }
    public string OtherUserId { get; set; } = string.Empty;
    public string OtherUserDisplayName { get; set; } = string.Empty;
    public string? LastMessage { get; set; }
    public DateTime? LastMessageTime { get; set; }
    public int UnreadCount { get; set; }
    public Guid ListingId { get; set; }
    public string ListingTitle { get; set; } = string.Empty;
}

