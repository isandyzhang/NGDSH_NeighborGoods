using System.ComponentModel.DataAnnotations;

namespace NeighborGoods.Web.Models.Enums;

/// <summary>
/// 商品所在社宅名稱。
/// </summary>
public enum ListingResidence
{
    [Display(Name = "未指定")]
    Unknown = 0,

    [Display(Name = "機廠")]
    Factory = 1,

    [Display(Name = "東明")]
    DongMing = 2,

    [Display(Name = "小彎")]
    XiaoWan = 3
}

