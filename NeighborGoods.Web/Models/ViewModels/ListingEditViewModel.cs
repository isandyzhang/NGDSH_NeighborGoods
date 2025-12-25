using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using NeighborGoods.Web.Models.Enums;

namespace NeighborGoods.Web.Models.ViewModels;

public class ListingEditViewModel
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "請填寫商品標題，讓大家更容易找到您的商品")]
    [Display(Name = "標題")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "請填寫商品描述，幫助大家更了解您的商品")]
    [Display(Name = "描述")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "請選擇商品類別，方便大家找到您的商品")]
    [Display(Name = "商品類別")]
    public ListingCategory Category { get; set; } = ListingCategory.Other;

    [Required(ErrorMessage = "請選擇商品新舊程度")]
    [Display(Name = "新舊程度")]
    public ListingCondition Condition { get; set; } = ListingCondition.Good;

    [Range(0, 999999, ErrorMessage = "價格請設定在 0 到 999,999 元之間")]
    [Display(Name = "價格")]
    public decimal Price { get; set; }

    [Display(Name = "免費索取")]
    public bool IsFree { get; set; }

    [Display(Name = "愛心商品")]
    public bool IsCharity { get; set; }

    [Required(ErrorMessage = "請選擇面交地點，方便買家與您聯繫")]
    [Display(Name = "面交地點")]
    public ListingPickupLocation PickupLocation { get; set; } = ListingPickupLocation.Message;

    /// <summary>
    /// 現有圖片 URL 列表
    /// </summary>
    public List<string> ExistingImages { get; set; } = new List<string>();

    /// <summary>
    /// 要刪除的圖片 URL 列表
    /// </summary>
    public List<string> ImagesToDelete { get; set; } = new List<string>();

    [Display(Name = "圖片 1")]
    public IFormFile? Image1 { get; set; }

    [Display(Name = "圖片 2")]
    public IFormFile? Image2 { get; set; }

    [Display(Name = "圖片 3")]
    public IFormFile? Image3 { get; set; }

    [Display(Name = "圖片 4")]
    public IFormFile? Image4 { get; set; }

    [Display(Name = "圖片 5")]
    public IFormFile? Image5 { get; set; }
}

