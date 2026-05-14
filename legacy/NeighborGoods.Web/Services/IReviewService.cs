using NeighborGoods.Web.Models.DTOs;
using NeighborGoods.Web.Models.ViewModels;

namespace NeighborGoods.Web.Services;

/// <summary>
/// 評價服務介面
/// </summary>
public interface IReviewService
{
    /// <summary>
    /// 提交評價
    /// </summary>
    Task<ServiceResult> SubmitReviewAsync(SubmitReviewViewModel model, string userId);

    /// <summary>
    /// 取得賣家檔案（包含評價和統計）
    /// </summary>
    Task<SellerProfileViewModel?> GetSellerProfileAsync(string sellerId);
}

