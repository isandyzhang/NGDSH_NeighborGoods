namespace NeighborGoods.Api.Features.Listing;

/// <summary>
/// 與 NeighborGoods.Web 的 ListingStatus 整數語意一致（Web 寫入之 DB 狀態欄）。
/// </summary>
public enum ListingStatus
{
    Active = 0,
    Reserved = 1,
    Sold = 2,
    Donated = 3,
    Inactive = 4,
    GivenOrTraded = 5
}
