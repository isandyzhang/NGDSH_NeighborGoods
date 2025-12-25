using System.ComponentModel.DataAnnotations;

namespace NeighborGoods.Web.Models.ViewModels;

public class SendMessageViewModel
{
    public Guid? ConversationId { get; set; }

    [Required(ErrorMessage = "訊息內容不能為空")]
    [StringLength(1000, ErrorMessage = "訊息內容不能超過 1000 個字元")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 接收者 UserId（用於建立新對話）
    /// </summary>
    public string? ReceiverId { get; set; }
}

