using NeighborGoods.Web.Models.Enums;

namespace NeighborGoods.Web.Models.ViewModels;

public class ListingDetailsViewModel
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ListingCategory Category { get; set; }

    public ListingCondition Condition { get; set; }

    public ListingPickupLocation PickupLocation { get; set; }

    public decimal Price { get; set; }

    public bool IsFree { get; set; }

    public bool IsCharity { get; set; }

    public ListingStatus Status { get; set; }

    /// <summary>
    /// 所有圖片 URL 列表，依 SortOrder 排序。
    /// </summary>
    public List<string> Images { get; set; } = new List<string>();

    /// <summary>
    /// 賣家使用者 Id。
    /// </summary>
    public string SellerId { get; set; } = string.Empty;

    /// <summary>
    /// 賣家顯示名稱。
    /// </summary>
    public string SellerDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 刊登時間（台灣時間）。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 最後更新時間（台灣時間）。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 是否顯示私訊按鈕。目前先設為 false，之後實作私訊功能時再啟用。
    /// </summary>
    public bool CanMessage { get; set; } = false;

    /// <summary>
    /// 是否為商品擁有者。
    /// </summary>
    public bool IsOwner { get; set; } = false;
}

