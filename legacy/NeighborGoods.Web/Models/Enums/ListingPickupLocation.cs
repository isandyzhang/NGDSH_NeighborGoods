using System.ComponentModel.DataAnnotations;

namespace NeighborGoods.Web.Models.Enums;

public enum ListingPickupLocation
{
    [Display(Name = "北棟管理室")]
    NorthBuilding = 0,      // 北棟管理室

    [Display(Name = "南棟管理室")]
    SouthBuilding = 1,      // 南棟管理室

    [Display(Name = "風雨操場")]
    Gym = 2,                // 風雨操場

    [Display(Name = "私訊")]
    Message = 3             // 私訊
}

