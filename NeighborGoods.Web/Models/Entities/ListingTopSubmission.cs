using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Models.Entities;

/// <summary>
/// 置頂投稿實體（使用者投稿合照換取置頂次數）
/// </summary>
public class ListingTopSubmission
{
    public int Id { get; set; }

    /// <summary>
    /// 投稿者使用者 Id（外鍵 → ApplicationUser）
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 投稿者導航屬性
    /// </summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// 對應交易的商品 Id（可選，可為 null 表示泛用投稿）
    /// </summary>
    public Guid? ListingId { get; set; }

    /// <summary>
    /// 對應商品的導航屬性
    /// </summary>
    public Listing? Listing { get; set; }

    /// <summary>
    /// 合照檔案在 Blob Storage 的檔名或完整 URL
    /// </summary>
    public string PhotoBlobName { get; set; } = string.Empty;

    /// <summary>
    /// 商品名稱或標題
    /// </summary>
    public string FeedbackTitle { get; set; } = string.Empty;

    /// <summary>
    /// 詳細回饋內容
    /// </summary>
    public string FeedbackDetail { get; set; } = string.Empty;

    /// <summary>
    /// 是否同意宣傳（可能會發到 FB）
    /// </summary>
    public bool AllowPromotion { get; set; }

    /// <summary>
    /// 審核狀態（Pending / Approved / Rejected）
    /// </summary>
    public TopSubmissionStatus Status { get; set; } = TopSubmissionStatus.Pending;

    /// <summary>
    /// 建立時間（台灣時間）
    /// </summary>
    public DateTime CreatedAt { get; set; } = TaiwanTime.Now;

    /// <summary>
    /// 審核時間（台灣時間，可為 null）
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// 審核的管理員 Id（外鍵 → ApplicationUser，可為 null）
    /// </summary>
    public string? ReviewedByAdminId { get; set; }

    /// <summary>
    /// 審核的管理員導航屬性
    /// </summary>
    public ApplicationUser? ReviewedByAdmin { get; set; }

    /// <summary>
    /// 本次投稿核准後給予的置頂次數（預設 7）
    /// </summary>
    public int GrantedCredits { get; set; } = 7;
}
