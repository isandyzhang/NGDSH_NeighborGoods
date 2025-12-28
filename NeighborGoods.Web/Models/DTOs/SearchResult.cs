namespace NeighborGoods.Web.Models.DTOs;

/// <summary>
/// 搜尋結果封裝類別
/// </summary>
public class SearchResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore => (Page * PageSize) < TotalCount;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

