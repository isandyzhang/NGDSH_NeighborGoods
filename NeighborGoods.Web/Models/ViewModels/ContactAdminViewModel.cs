using System.ComponentModel.DataAnnotations;

namespace NeighborGoods.Web.Models.ViewModels;

public class ContactAdminViewModel
{
    [Required(ErrorMessage = "請輸入訊息內容")]
    [StringLength(1000, ErrorMessage = "訊息內容不能超過 1000 字")]
    [Display(Name = "訊息內容")]
    public string Content { get; set; } = string.Empty;
}

