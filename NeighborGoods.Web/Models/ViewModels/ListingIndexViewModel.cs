using NeighborGoods.Web.Models.Enums;

namespace NeighborGoods.Web.Models.ViewModels;

public class ListingIndexViewModel
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public ListingCategory Category { get; set; }

    public ListingCondition? Condition { get; set; }

    public decimal Price { get; set; }

    public bool IsFree { get; set; }

    public bool IsCharity { get; set; }

    public ListingStatus Status { get; set; }

    /// <summary>
    /// 第一張圖片 URL，用於卡片顯示。若無圖片則為 null。
    /// </summary>
    public string? FirstImageUrl { get; set; }

    /// <summary>
    /// 賣家顯示名稱。
    /// </summary>
    public string SellerDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 刊登時間（台灣時間）。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 對此商品有興趣的人數（基於該商品的對話數量）。
    /// </summary>
    public int InterestCount { get; set; }
}

