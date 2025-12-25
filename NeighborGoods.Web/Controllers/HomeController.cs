using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Web.Data;
using NeighborGoods.Web.Infrastructure;
using NeighborGoods.Web.Models;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Models.ViewModels;

namespace NeighborGoods.Web.Controllers;

public class HomeController : BaseController
{
    private readonly ILogger<HomeController> _logger;
    private readonly AppDbContext _db;

    public HomeController(
        ILogger<HomeController> logger, 
        AppDbContext db,
        UserManager<ApplicationUser> userManager)
        : base(userManager)
    {
        _logger = logger;
        _db = db;
    }

    public async Task<IActionResult> Index(
        string? search,
        ListingCategory? category,
        ListingCondition? condition,
        int? minPrice,
        int? maxPrice,
        bool? isFree,
        bool? isCharity,
        int page = 1)
    {
        // 取得當前登入用戶（如果有的話）
        var currentUser = await GetCurrentUserAsync();
        var currentUserId = currentUser?.Id;

        // 建立查詢基礎（不使用 Include，改用投影查詢）
        var query = _db.Listings
            .Where(l => l.Status == ListingStatus.Active);

        // 如果用戶已登入，排除自己的商品
        if (!string.IsNullOrEmpty(currentUserId))
        {
            query = query.Where(l => l.SellerId != currentUserId);
        }

        // 關鍵字搜尋（標題或描述）
        // 優化：限制搜尋長度，避免過短的搜尋詞影響效能
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            // 至少需要 2 個字元才進行搜尋
            if (searchTerm.Length >= 2)
            {
                query = query.Where(l => l.Title.Contains(searchTerm) || l.Description.Contains(searchTerm));
            }
        }

        // 分類篩選
        if (category.HasValue)
        {
            query = query.Where(l => l.Category == category.Value);
        }

        // 新舊程度篩選
        if (condition.HasValue)
        {
            query = query.Where(l => l.Condition == condition.Value);
        }

        // 價格範圍篩選
        if (minPrice.HasValue)
        {
            query = query.Where(l => l.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(l => l.Price <= maxPrice.Value);
        }

        // 免費商品篩選
        if (isFree == true)
        {
            query = query.Where(l => l.IsFree == true);
        }

        // 愛心商品篩選
        if (isCharity == true)
        {
            query = query.Where(l => l.IsCharity == true);
        }

        // 計算總數
        var totalCount = await query.CountAsync();

        // 使用投影查詢，只選擇需要的欄位
        // 注意：在投影查詢中，EF Core 會自動處理關聯，不需要 Include
        var viewModels = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * 10)
            .Take(10)
            .Select(l => new ListingIndexViewModel
            {
                Id = l.Id,
                Title = l.Title,
                Category = l.Category,
                Condition = l.Condition,
                Price = l.Price,
                IsFree = l.IsFree,
                IsCharity = l.IsCharity,
                Status = l.Status,
                CreatedAt = l.CreatedAt,
                // 只取第一張圖片的 URL（資料庫端處理）
                FirstImageUrl = l.Images
                    .OrderBy(img => img.SortOrder)
                    .Select(img => img.ImageUrl)
                    .FirstOrDefault(),
                // 只取賣家顯示名稱（EF Core 會自動處理關聯查詢）
                SellerDisplayName = l.Seller != null ? (l.Seller.DisplayName ?? "未知賣家") : "未知賣家"
            })
            .ToListAsync();

        // 建立搜尋 ViewModel
        var searchViewModel = new ListingSearchViewModel
        {
            Search = search,
            Category = category,
            Condition = condition,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            IsFree = isFree,
            IsCharity = isCharity,
            Listings = viewModels,
            Page = page,
            PageSize = 10,
            TotalCount = totalCount
        };

        // 如果是 AJAX 請求，返回 JSON
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new
            {
                listings = viewModels,
                hasMore = searchViewModel.HasMore,
                page = page,
                totalCount = totalCount
            });
        }

        return View(searchViewModel);
    }

    public IActionResult Privacy()
    {
        ViewData["Title"] = "隱私條款";
        return View();
    }

    public IActionResult Terms()
    {
        ViewData["Title"] = "使用條款";
        return View();
    }

    /// <summary>
    /// 計算當前用戶的未讀訊息數量
    /// 邏輯：計算所有對話中，在最後已讀時間之後且不是由當前用戶發送的訊息數量
    /// 優化：使用單一查詢避免 N+1 問題
    /// </summary>
    public async Task<int> GetUnreadMessageCountAsync(string userId)
    {
        try
        {
            // 使用單一查詢計算所有對話的未讀數量
            // 分別處理 Participant1 和 Participant2 的情況
            var unreadCount1 = await (
                from c in _db.Conversations
                where c.Participant1Id == userId
                from m in _db.Messages
                where m.ConversationId == c.Id
                    && m.SenderId != userId
                    && (c.Participant1LastReadAt == null || m.CreatedAt > c.Participant1LastReadAt.Value)
                select m
            ).CountAsync();

            var unreadCount2 = await (
                from c in _db.Conversations
                where c.Participant2Id == userId
                from m in _db.Messages
                where m.ConversationId == c.Id
                    && m.SenderId != userId
                    && (c.Participant2LastReadAt == null || m.CreatedAt > c.Participant2LastReadAt.Value)
                select m
            ).CountAsync();

            return unreadCount1 + unreadCount2;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "計算未讀訊息數量時發生錯誤");
            return 0;
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
