using Microsoft.EntityFrameworkCore;
using NeighborGoods.Web.Data;
using NeighborGoods.Web.Models.DTOs;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Services;

/// <summary>
/// 置頂投稿審核服務實作
/// </summary>
public class TopListingService : ITopListingService
{
    private readonly AppDbContext _dbContext;

    public TopListingService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult> ApproveSubmissionAsync(int submissionId, int grantedCredits, string adminId)
    {
        var submission = await _dbContext.ListingTopSubmissions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == submissionId);

        if (submission == null)
        {
            return ServiceResult.Fail("找不到投稿記錄");
        }

        if (submission.Status != TopSubmissionStatus.Pending)
        {
            return ServiceResult.Fail("此投稿已經審核過了");
        }

        if (submission.User == null)
        {
            return ServiceResult.Fail("找不到投稿者");
        }

        // 更新投稿狀態
        submission.Status = TopSubmissionStatus.Approved;
        submission.ReviewedAt = TaiwanTime.Now;
        submission.ReviewedByAdminId = adminId;
        submission.GrantedCredits = grantedCredits;

        // 增加使用者的置頂次數
        submission.User.TopPinCredits += grantedCredits;

        try
        {
            await _dbContext.SaveChangesAsync();
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            return ServiceResult.Fail($"審核失敗：{ex.Message}");
        }
    }

    public async Task<ServiceResult> RejectSubmissionAsync(int submissionId, string adminId, string? reason = null)
    {
        var submission = await _dbContext.ListingTopSubmissions
            .FirstOrDefaultAsync(s => s.Id == submissionId);

        if (submission == null)
        {
            return ServiceResult.Fail("找不到投稿記錄");
        }

        if (submission.Status != TopSubmissionStatus.Pending)
        {
            return ServiceResult.Fail("此投稿已經審核過了");
        }

        // 更新投稿狀態
        submission.Status = TopSubmissionStatus.Rejected;
        submission.ReviewedAt = TaiwanTime.Now;
        submission.ReviewedByAdminId = adminId;

        try
        {
            await _dbContext.SaveChangesAsync();
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            return ServiceResult.Fail($"駁回失敗：{ex.Message}");
        }
    }
}
