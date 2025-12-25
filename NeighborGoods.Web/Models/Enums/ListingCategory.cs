using System.ComponentModel.DataAnnotations;

namespace NeighborGoods.Web.Models.Enums;

public enum ListingCategory
{
    [Display(Name = "家具家飾")]
    Furniture = 0,      // 家具家飾

    [Display(Name = "電子產品")]
    Electronics = 1,     // 電子產品

    [Display(Name = "服飾配件")]
    Clothing = 2,       // 服飾配件

    [Display(Name = "書籍文具")]
    Books = 3,          // 書籍文具

    [Display(Name = "運動用品")]
    Sports = 4,         // 運動用品

    [Display(Name = "玩具遊戲")]
    Toys = 5,           // 玩具遊戲

    [Display(Name = "廚房用品")]
    Kitchen = 6,        // 廚房用品

    [Display(Name = "生活用品")]
    Daily = 7,         // 生活用品

    [Display(Name = "嬰幼兒用品")]
    Baby = 8,          // 嬰幼兒用品

    [Display(Name = "其他")]
    Other = 9           // 其他
}

