using System.ComponentModel.DataAnnotations;

namespace NeighborGoods.Web.Models.Enums;

public enum ListingCondition
{
    [Display(Name = "全新")]
    New = 0,           // 全新

    [Display(Name = "近全新")]
    LikeNew = 1,       // 近全新

    [Display(Name = "良好")]
    Good = 2,          // 良好

    [Display(Name = "普通")]
    Fair = 3,          // 普通

    [Display(Name = "歲月痕跡")]
    WellUsed = 4       // 歲月痕跡
}

