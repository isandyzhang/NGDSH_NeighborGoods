using NeighborGoods.Web.Models.DTOs;

namespace NeighborGoods.Web.Services;

/// <summary>
/// 置頂投稿審核服務介面
/// </summary>
public interface ITopListingService
{
    /// <summary>
    /// 核准投稿，給予使用者置頂次數
    /// </summary>
    /// <param name="submissionId">投稿 ID</param>
    /// <param name="grantedCredits">給予的置頂次數（預設 7）</param>
    /// <param name="adminId">審核的管理員 ID</param>
    /// <returns>操作結果</returns>
    Task<ServiceResult> ApproveSubmissionAsync(int submissionId, int grantedCredits, string adminId);

    /// <summary>
    /// 駁回投稿
    /// </summary>
    /// <param name="submissionId">投稿 ID</param>
    /// <param name="adminId">審核的管理員 ID</param>
    /// <param name="reason">駁回原因（可選）</param>
    /// <returns>操作結果</returns>
    Task<ServiceResult> RejectSubmissionAsync(int submissionId, string adminId, string? reason = null);
}
