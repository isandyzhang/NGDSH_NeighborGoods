using System.ComponentModel.DataAnnotations;

namespace NeighborGoods.Web.Models.ViewModels;

public class RegisterViewModel
{
    [Required]
    [Display(Name = "帳號")]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [EmailAddress(ErrorMessage = "請輸入有效的 Email 地址")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(50, ErrorMessage = "顯示名稱不能超過 50 個字元")]
    [Display(Name = "顯示名稱")]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "密碼")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "確認密碼")]
    [Compare("Password", ErrorMessage = "兩次輸入的密碼不一致")]
    public string ConfirmPassword { get; set; } = string.Empty;
}


