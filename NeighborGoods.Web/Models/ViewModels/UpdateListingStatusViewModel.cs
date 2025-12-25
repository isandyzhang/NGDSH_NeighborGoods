using System.ComponentModel.DataAnnotations;
using NeighborGoods.Web.Models.Enums;

namespace NeighborGoods.Web.Models.ViewModels;

public class UpdateListingStatusViewModel
{
    public Guid Id { get; set; }
    
    public string Title { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "請選擇商品狀態")]
    [Display(Name = "商品狀態")]
    public ListingStatus Status { get; set; }
}

