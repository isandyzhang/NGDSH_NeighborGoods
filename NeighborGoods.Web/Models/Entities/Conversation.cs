using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Models.Entities;

public class Conversation
{
    public Guid Id { get; set; }

    /// <summary>
    /// 參與者1的 UserId
    /// </summary>
    public string Participant1Id { get; set; } = string.Empty;

    /// <summary>
    /// 參與者2的 UserId
    /// </summary>
    public string Participant2Id { get; set; } = string.Empty;

    /// <summary>
    /// 參與者1的導航屬性
    /// </summary>
    public ApplicationUser? Participant1 { get; set; }

    /// <summary>
    /// 參與者2的導航屬性
    /// </summary>
    public ApplicationUser? Participant2 { get; set; }

    /// <summary>
    /// 對話中的所有訊息
    /// </summary>
    public ICollection<Message> Messages { get; set; } = new List<Message>();

    /// <summary>
    /// 建立時間（台灣時間）
    /// </summary>
    public DateTime CreatedAt { get; set; } = TaiwanTime.Now;

    /// <summary>
    /// 最後更新時間（台灣時間）
    /// </summary>
    public DateTime UpdatedAt { get; set; } = TaiwanTime.Now;

    /// <summary>
    /// 參與者1最後已讀時間（台灣時間）
    /// </summary>
    public DateTime? Participant1LastReadAt { get; set; }

    /// <summary>
    /// 參與者2最後已讀時間（台灣時間）
    /// </summary>
    public DateTime? Participant2LastReadAt { get; set; }

    /// <summary>
    /// 關聯的商品 ID（所有對話都必須關聯商品）
    /// </summary>
    public Guid ListingId { get; set; }

    /// <summary>
    /// 商品的導航屬性
    /// </summary>
    public Listing? Listing { get; set; }
}

