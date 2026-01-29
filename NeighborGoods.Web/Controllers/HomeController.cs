using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NeighborGoods.Web.Constants;
using NeighborGoods.Web.Infrastructure;
using NeighborGoods.Web.Models;
using NeighborGoods.Web.Models.DTOs;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Models.ViewModels;
using NeighborGoods.Web.Services;

namespace NeighborGoods.Web.Controllers;

public class HomeController : BaseController
{
    private readonly ILogger<HomeController> _logger;
    private readonly IListingService _listingService;

    public HomeController(
        ILogger<HomeController> logger,
        UserManager<ApplicationUser> userManager,
        IListingService listingService)
        : base(userManager)
    {
        _logger = logger;
        _listingService = listingService;
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

        // 建立搜尋條件
        var criteria = new ListingSearchCriteria
        {
            SearchTerm = search,
            Category = category,
            Condition = condition,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            IsFree = isFree,
            IsCharity = isCharity,
            ExcludeUserId = currentUserId
        };

        // 使用服務層搜尋商品（每頁 12 筆 ≈ 電腦版 3 行 × 4 欄）
        var result = await _listingService.SearchListingsAsync(
            criteria,
            currentUserId,
            page,
            PaginationConstants.HomeListingPageSize);

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
            Listings = result.Items,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount
        };

        // 如果是 AJAX 請求，返回 JSON
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new
            {
                listings = result.Items,
                hasMore = result.HasMore,
                page = result.Page,
                totalCount = result.TotalCount
            });
        }

        // 檢查用戶的 Email 通知狀態
        if (currentUser != null)
        {
            ViewBag.IsEmailNotificationEnabled = currentUser.EmailNotificationEnabled && 
                                                 !string.IsNullOrEmpty(currentUser.Email) && 
                                                 currentUser.EmailConfirmed;
            ViewBag.HasEmail = !string.IsNullOrEmpty(currentUser.Email) && currentUser.EmailConfirmed;
        }
        else
        {
            ViewBag.IsEmailNotificationEnabled = false;
            ViewBag.HasEmail = false;
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


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
