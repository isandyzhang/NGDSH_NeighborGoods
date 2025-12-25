using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Web.Data;
using NeighborGoods.Web.Models;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Models.ViewModels;

namespace NeighborGoods.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(ILogger<HomeController> logger, AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _logger = logger;
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(
        string? search,
        ListingCategory? category,
        ListingCondition? condition,
        int? minPrice,
        int? maxPrice,
        bool? isFree,
        bool? isCharity)
    {
        // 建立查詢
        var query = _db.Listings
            .Include(l => l.Images)
            .Include(l => l.Seller)
            .Where(l => l.Status == ListingStatus.Active);

        // 關鍵字搜尋（標題或描述）
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            query = query.Where(l => l.Title.Contains(searchTerm) || l.Description.Contains(searchTerm));
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

        // 執行查詢並排序
        var listings = await query
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        // 轉換為 ViewModel
        var viewModels = listings.Select(l => new ListingIndexViewModel
        {
            Id = l.Id,
            Title = l.Title,
            Category = l.Category,
            Condition = l.Condition,
            Price = l.Price,
            IsFree = l.IsFree,
            IsCharity = l.IsCharity,
            Status = l.Status,
            FirstImageUrl = l.Images
                .OrderBy(img => img.SortOrder)
                .FirstOrDefault()?.ImageUrl,
            SellerDisplayName = l.Seller?.DisplayName ?? "未知賣家",
            CreatedAt = l.CreatedAt
        }).ToList();

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
            Listings = viewModels
        };

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
    /// 邏輯：計算所有對話中，最後一則訊息不是由當前用戶發送的對話數量
    /// </summary>
    public async Task<int> GetUnreadMessageCountAsync(string userId)
    {
        try
        {
            var conversations = await _db.Conversations
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
                .Where(c => c.Participant1Id == userId || c.Participant2Id == userId)
                .ToListAsync();

            int unreadCount = 0;
            foreach (var conversation in conversations)
            {
                var lastMessage = conversation.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefault();

                // 如果最後一則訊息存在且不是當前用戶發送的，則視為有未讀訊息
                if (lastMessage != null && lastMessage.SenderId != userId)
                {
                    unreadCount++;
                }
            }

            return unreadCount;
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
