using NeighborGoods.Web.Models.Enums;

namespace NeighborGoods.Web.Models.ViewModels;

public class ListingSearchViewModel
{
    /// <summary>
    /// 搜尋關鍵字（標題或描述）
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// 商品分類篩選
    /// </summary>
    public ListingCategory? Category { get; set; }

    /// <summary>
    /// 新舊程度篩選
    /// </summary>
    public ListingCondition? Condition { get; set; }

    /// <summary>
    /// 社宅名稱篩選
    /// </summary>
    public ListingResidence? Residence { get; set; }

    /// <summary>
    /// 最低價格
    /// </summary>
    public int? MinPrice { get; set; }

    /// <summary>
    /// 最高價格
    /// </summary>
    public int? MaxPrice { get; set; }

    /// <summary>
    /// 只看免費商品
    /// </summary>
    public bool? IsFree { get; set; }

    /// <summary>
    /// 只看愛心商品
    /// </summary>
    public bool? IsCharity { get; set; }

    /// <summary>
    /// 搜尋結果列表
    /// </summary>
    public List<ListingIndexViewModel> Listings { get; set; } = new List<ListingIndexViewModel>();

    /// <summary>
    /// 當前頁碼
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// 每頁數量
    /// </summary>
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// 總商品數
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 是否還有更多商品
    /// </summary>
    public bool HasMore => (Page * PageSize) < TotalCount;
}

