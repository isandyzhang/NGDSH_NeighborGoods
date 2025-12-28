using NeighborGoods.Web.Models.Enums;

namespace NeighborGoods.Web.Models.DTOs;

/// <summary>
/// 商品搜尋條件
/// </summary>
public class ListingSearchCriteria
{
    public string? SearchTerm { get; set; }
    public ListingCategory? Category { get; set; }
    public ListingCondition? Condition { get; set; }
    public int? MinPrice { get; set; }
    public int? MaxPrice { get; set; }
    public bool? IsFree { get; set; }
    public bool? IsCharity { get; set; }
    public string? ExcludeUserId { get; set; }
}

