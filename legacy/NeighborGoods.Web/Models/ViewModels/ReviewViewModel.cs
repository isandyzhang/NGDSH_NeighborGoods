using System.ComponentModel.DataAnnotations;

namespace NeighborGoods.Web.Models.ViewModels;

public class ReviewViewModel
{
    public Guid ListingId { get; set; }
    public Guid ConversationId { get; set; }
    public string ListingTitle { get; set; } = string.Empty;
    public string OtherUserId { get; set; } = string.Empty;
    public string OtherUserDisplayName { get; set; } = string.Empty;
    public bool IsBuyerReviewingSeller { get; set; }
}

public class SubmitReviewViewModel
{
    public Guid ListingId { get; set; }
    public Guid ConversationId { get; set; }
    public string TargetUserId { get; set; } = string.Empty;
    public int Rating { get; set; }
    
    [StringLength(500, ErrorMessage = "評價內容不能超過 500 個字元")]
    public string? Content { get; set; }
}

