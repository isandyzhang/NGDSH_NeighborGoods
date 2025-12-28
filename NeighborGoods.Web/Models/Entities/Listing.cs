using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Models.Entities;

public class Listing
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 商品價格，單位：元。免費商品時為 0。
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// 是否免費索取（免費商品）。
    /// </summary>
    public bool IsFree { get; set; }

    /// <summary>
    /// 是否為愛心商品（可搭配 IsFree 使用）。
    /// </summary>
    public bool IsCharity { get; set; }

    /// <summary>
    /// 商品狀態（上架中 / 保留中 / 已售出 / 已捐贈 / 已下架）。
    /// </summary>
    public ListingStatus Status { get; set; } = ListingStatus.Active;

    /// <summary>
    /// 商品類別（家具家飾 / 電子產品 / 服飾配件 / 書籍文具 / 運動用品 / 玩具遊戲 / 廚房用品 / 生活用品 / 嬰幼兒用品 / 其他）。
    /// </summary>
    public ListingCategory Category { get; set; } = ListingCategory.Other;

    /// <summary>
    /// 商品新舊程度（全新 / 近全新 / 良好 / 普通 / 歲月痕跡）。
    /// </summary>
    public ListingCondition Condition { get; set; } = ListingCondition.Good;

    /// <summary>
    /// 面交地點（北棟管理室 / 南棟管理室 / 風雨操場 / 私訊）。
    /// </summary>
    public ListingPickupLocation PickupLocation { get; set; } = ListingPickupLocation.Message;

    /// <summary>
    /// 賣家使用者 Id（外鍵 → ApplicationUser）。
    /// </summary>
    public string SellerId { get; set; } = string.Empty;

    public ApplicationUser? Seller { get; set; }

    /// <summary>
    /// 買家使用者 Id（外鍵 → ApplicationUser，交易完成時記錄）。
    /// </summary>
    public string? BuyerId { get; set; }

    /// <summary>
    /// 買家的導航屬性。
    /// </summary>
    public ApplicationUser? Buyer { get; set; }

    public ICollection<ListingImage> Images { get; set; } = new List<ListingImage>();

    /// <summary>
    /// 建立時間（台灣時間）。
    /// </summary>
    public DateTime CreatedAt { get; set; } = TaiwanTime.Now;

    /// <summary>
    /// 最後更新時間（台灣時間）。
    /// </summary>
    public DateTime UpdatedAt { get; set; } = TaiwanTime.Now;
}


