using System.ComponentModel.DataAnnotations;

namespace NeighborGoods.Web.Models.ViewModels;

public class SendMessageViewModel
{
    public Guid? ConversationId { get; set; }

    [Required(ErrorMessage = "訊息內容不能為空")]
    [StringLength(50, ErrorMessage = "訊息內容不能超過 50 個字元")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 接收者 UserId（用於建立新對話）
    /// </summary>
    public string? ReceiverId { get; set; }

    /// <summary>
    /// 商品 ID（用於建立新對話時，所有對話都必須關聯商品）
    /// </summary>
    public Guid? ListingId { get; set; }
}

