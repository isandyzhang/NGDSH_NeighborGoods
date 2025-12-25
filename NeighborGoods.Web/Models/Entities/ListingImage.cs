using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Models.Entities;

public class ListingImage
{
    public Guid Id { get; set; }

    public Guid ListingId { get; set; }

    public Listing Listing { get; set; } = null!;

    /// <summary>
    /// 圖片在 Blob Storage 中的完整 URL 或相對路徑。
    /// </summary>
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// 排序用索引（0-4），一個商品最多 5 張。
    /// </summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = TaiwanTime.Now;
}


