using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Models.Entities;

/// <summary>
/// 交易評價（買家評價賣家）
/// 一個商品只能有一筆評價（ListingId 唯一）
/// </summary>
public class Review
{
    public Guid Id { get; set; }

    /// <summary>
    /// 商品 Id（外鍵 → Listing）
    /// 唯一約束：一個商品只能有一筆評價
    /// </summary>
    public Guid ListingId { get; set; }

    /// <summary>
    /// 商品的導航屬性
    /// </summary>
    public Listing? Listing { get; set; }

    /// <summary>
    /// 賣家使用者 Id（外鍵 → ApplicationUser）
    /// </summary>
    public string SellerId { get; set; } = string.Empty;

    /// <summary>
    /// 賣家的導航屬性
    /// </summary>
    public ApplicationUser? Seller { get; set; }

    /// <summary>
    /// 買家/評價者使用者 Id（外鍵 → ApplicationUser）
    /// </summary>
    public string BuyerId { get; set; } = string.Empty;

    /// <summary>
    /// 買家的導航屬性
    /// </summary>
    public ApplicationUser? Buyer { get; set; }

    /// <summary>
    /// 評分（1-5 星）
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// 評價內容（選填）
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// 評價時間（台灣時間）
    /// </summary>
    public DateTime CreatedAt { get; set; } = TaiwanTime.Now;
}

