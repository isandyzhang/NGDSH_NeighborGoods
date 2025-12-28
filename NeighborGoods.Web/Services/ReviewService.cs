using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeighborGoods.Web.Data;
using NeighborGoods.Web.Models.DTOs;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Models.ViewModels;
using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Services;

/// <summary>
/// 評價服務實作
/// </summary>
public class ReviewService : IReviewService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(
        AppDbContext db,
        ILogger<ReviewService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ServiceResult> SubmitReviewAsync(SubmitReviewViewModel model, string userId)
    {
        try
        {
            // 驗證評分範圍
            if (model.Rating < 1 || model.Rating > 5)
            {
                return ServiceResult.Fail("評分必須在 1-5 之間");
            }

            // 驗證商品
            var listing = await _db.Listings
                .FirstOrDefaultAsync(l => l.Id == model.ListingId);

            if (listing == null)
            {
                return ServiceResult.Fail("找不到商品");
            }

            // 驗證商品狀態為已售出
            if (listing.Status != ListingStatus.Sold && listing.Status != ListingStatus.Donated)
            {
                return ServiceResult.Fail("只有已售出或已捐贈的商品才能評價");
            }

            // 驗證對話
            var conversation = await _db.Conversations
                .FirstOrDefaultAsync(c => c.Id == model.ConversationId);

            if (conversation == null)
            {
                return ServiceResult.Fail("找不到對話");
            }

            // 驗證當前用戶是否為對話參與者
            if (conversation.Participant1Id != userId && conversation.Participant2Id != userId)
            {
                return ServiceResult.Fail("無權限訪問此對話");
            }

            // 判斷當前用戶是買家還是賣家
            var isBuyer = listing.SellerId != userId;

            // 確定被評價者
            var targetUserId = isBuyer
                ? listing.SellerId  // 買家評價賣家
                : (conversation.Participant1Id == userId ? conversation.Participant2Id : conversation.Participant1Id); // 賣家評價買家

            // 檢查是否已經評價過
            var existingReview = await _db.Reviews
                .FirstOrDefaultAsync(r => r.ListingId == model.ListingId && r.BuyerId == userId);

            if (existingReview != null)
            {
                // 更新現有評價
                existingReview.Rating = model.Rating;
                existingReview.Content = model.Content?.Trim();
                existingReview.CreatedAt = TaiwanTime.Now;
            }
            else
            {
                // 創建新評價
                var review = new Review
                {
                    Id = Guid.NewGuid(),
                    ListingId = listing.Id,
                    SellerId = targetUserId, // 被評價者
                    BuyerId = userId, // 評價者
                    Rating = model.Rating,
                    Content = model.Content?.Trim(),
                    CreatedAt = TaiwanTime.Now
                };

                _db.Reviews.Add(review);
            }

            await _db.SaveChangesAsync();

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提交評價時發生錯誤");
            return ServiceResult.Fail("提交評價時發生錯誤，請稍後再試");
        }
    }

    public async Task<SellerProfileViewModel?> GetSellerProfileAsync(string sellerId)
    {
        try
        {
            // 查詢賣家的所有有評價的交易（透過 Review 記錄查詢）
            var reviews = await _db.Reviews
                .Include(r => r.Listing)
                .Include(r => r.Buyer)
                .Where(r => r.SellerId == sellerId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // 計算統計數據
            var totalCompletedTransactions = reviews.Count;
            var averageRating = totalCompletedTransactions > 0
                ? reviews.Average(r => (double)r.Rating)
                : 0.0;

            // 構建成交紀錄列表
            var completedTransactions = reviews.Select(r => new CompletedTransactionItem
            {
                ListingId = r.ListingId,
                ListingTitle = r.Listing?.Title ?? "未知商品",
                Price = r.Listing?.Price,
                IsFree = r.Listing?.IsFree ?? false,
                Rating = r.Rating,
                ReviewContent = r.Content,
                CompletedAt = r.CreatedAt,
                BuyerDisplayName = r.Buyer?.DisplayName ?? "未知買家"
            }).ToList();

            return new SellerProfileViewModel
            {
                SellerId = sellerId,
                SellerDisplayName = string.Empty, // 將由 Controller 填入
                TotalCompletedTransactions = totalCompletedTransactions,
                AverageRating = averageRating,
                CompletedTransactions = completedTransactions
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得賣家檔案 {SellerId} 時發生錯誤", sellerId);
            return null;
        }
    }
}

