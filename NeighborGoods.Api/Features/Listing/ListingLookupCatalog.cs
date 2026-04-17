using NeighborGoods.Api.Shared.Contracts;

namespace NeighborGoods.Api.Features.Listing;

public static class ListingLookupCatalog
{
    public static readonly IReadOnlyList<LookupItem> Categories =
    [
        new(0, "Furniture", "家具家飾"),
        new(1, "Electronics", "電子產品"),
        new(2, "Clothing", "服飾配件"),
        new(3, "Books", "書籍文具"),
        new(4, "Sports", "運動用品"),
        new(5, "Toys", "玩具遊戲"),
        new(6, "Kitchen", "廚房用品"),
        new(7, "Daily", "生活用品"),
        new(8, "Baby", "嬰幼兒用品"),
        new(9, "Other", "其他")
    ];

    public static readonly IReadOnlyList<LookupItem> Conditions =
    [
        new(0, "New", "全新"),
        new(1, "LikeNew", "近全新"),
        new(2, "Good", "良好"),
        new(3, "Fair", "普通"),
        new(4, "WellUsed", "歲月痕跡")
    ];

    public static readonly IReadOnlyList<LookupItem> Residences =
    [
        new(0, "Unknown", "未指定"),
        new(1, "Factory", "機廠"),
        new(2, "DongMing", "東明"),
        new(3, "XiaoWan", "小彎")
    ];
}
