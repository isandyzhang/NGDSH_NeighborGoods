using NeighborGoods.Web.Models.Enums;

namespace NeighborGoods.Web.Models.ViewModels;

public class ChatViewModel
{
    public Guid ConversationId { get; set; }
    public string CurrentUserId { get; set; } = string.Empty;
    public string OtherUserId { get; set; } = string.Empty;
    public string OtherUserDisplayName { get; set; } = string.Empty;
    public List<MessageViewModel> Messages { get; set; } = new List<MessageViewModel>();
    
    // 商品資訊（可選）
    public Guid? ListingId { get; set; }
    public string? ListingTitle { get; set; }
    public decimal? ListingPrice { get; set; }
    public string? ListingFirstImageUrl { get; set; }
    public ListingStatus? ListingStatus { get; set; }
    public bool? ListingIsFree { get; set; }
    public bool? ListingIsCharity { get; set; }
    public bool? ListingIsTradeable { get; set; }
    public string? ListingSellerId { get; set; }
    
    /// <summary>
    /// 當前用戶是否為商品賣家
    /// </summary>
    public bool IsSeller { get; set; }
    
    /// <summary>
    /// 當前用戶是否已對此交易評價過
    /// </summary>
    public bool HasCurrentUserReviewed { get; set; }
}

