using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Models.Entities;

public class Message
{
    public Guid Id { get; set; }

    /// <summary>
    /// 所屬對話的 Id
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// 對話的導航屬性
    /// </summary>
    public Conversation? Conversation { get; set; }

    /// <summary>
    /// 發送者的 UserId
    /// </summary>
    public string SenderId { get; set; } = string.Empty;

    /// <summary>
    /// 發送者的導航屬性
    /// </summary>
    public ApplicationUser? Sender { get; set; }

    /// <summary>
    /// 訊息內容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 發送時間（台灣時間）
    /// </summary>
    public DateTime CreatedAt { get; set; } = TaiwanTime.Now;
}

