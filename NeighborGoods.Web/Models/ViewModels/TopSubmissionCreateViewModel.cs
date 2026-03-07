using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace NeighborGoods.Web.Models.ViewModels;

/// <summary>
/// 置頂投稿建立 ViewModel
/// </summary>
public class TopSubmissionCreateViewModel
{
    /// <summary>
    /// 對應交易的商品 Id（可選）
    /// </summary>
    [Display(Name = "商品")]
    public Guid? ListingId { get; set; }

    /// <summary>
    /// 商品名稱或標題
    /// </summary>
    [Required(ErrorMessage = "請輸入商品名稱")]
    [Display(Name = "商品名稱")]
    [StringLength(200, ErrorMessage = "商品名稱不能超過 200 個字元")]
    public string FeedbackTitle { get; set; } = string.Empty;

    /// <summary>
    /// 詳細回饋內容
    /// </summary>
    [Required(ErrorMessage = "請輸入詳細回饋")]
    [Display(Name = "詳細回饋")]
    [StringLength(1000, ErrorMessage = "詳細回饋不能超過 1000 個字元")]
    public string FeedbackDetail { get; set; } = string.Empty;

    /// <summary>
    /// 合照檔案（必填）
    /// </summary>
    [Required(ErrorMessage = "請上傳合照照片")]
    [Display(Name = "合照照片")]
    public IFormFile Photo { get; set; } = null!;

    /// <summary>
    /// 是否同意宣傳（可能會發到 FB）
    /// </summary>
    [Display(Name = "同意宣傳")]
    public bool AllowPromotion { get; set; }
}
