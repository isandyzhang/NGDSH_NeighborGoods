using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Web.Constants;
using NeighborGoods.Web.Data;
using NeighborGoods.Web.Infrastructure;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Models.ViewModels;
using NeighborGoods.Web.Services;

namespace NeighborGoods.Web.Controllers;

[Authorize]
public class ListingController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IListingService _listingService;
    private readonly ILogger<ListingController> _logger;

    public ListingController(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IListingService listingService,
        ILogger<ListingController> logger)
        : base(userManager)
    {
        _db = db;
        _listingService = listingService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new ListingCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ListingCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Challenge(); // 要求重新登入
        }

        // 使用服務層建立商品
        var result = await _listingService.CreateListingAsync(model, user.Id);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "建立商品時發生錯誤");
            return View(model);
        }

        // 設定成功訊息
        TempData["SuccessMessage"] = "商品已新增成功！您可以前往「我的商品」查看。";
        
        // 之後會導到「商品詳情」或「我的商品」，暫時先回首頁
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var listing = await _db.Listings
            .Include(l => l.Images)
            .Include(l => l.Seller)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (listing == null)
        {
            TempData["ErrorMessage"] = "該商品不存在或已被刪除";
            return RedirectToAction("Index", "Home");
        }

        // 判斷是否為自己的商品
        var currentUser = await GetCurrentUserAsync();
        var isOwner = currentUser != null && listing.SellerId == currentUser.Id;
        
        // 如果用戶已登入且不是商品擁有者，可以發送訊息
        var canMessage = currentUser != null && !isOwner;

        // 查詢賣家的統計資訊（只計算有評價的交易）
        var sellerReviews = await _db.Reviews
            .Where(r => r.SellerId == listing.SellerId)
            .ToListAsync();

        var sellerTotalCompletedTransactions = sellerReviews.Count;
        var sellerAverageRating = sellerTotalCompletedTransactions > 0
            ? sellerReviews.Average(r => (double)r.Rating)
            : 0.0;

        var viewModel = new ListingDetailsViewModel
        {
            Id = listing.Id,
            Title = listing.Title,
            Description = listing.Description,
            Category = listing.Category,
            Condition = listing.Condition,
            PickupLocation = listing.PickupLocation,
            Price = listing.Price,
            IsFree = listing.IsFree,
            IsCharity = listing.IsCharity,
            Status = listing.Status,
            Images = listing.Images
                .OrderBy(img => img.SortOrder)
                .Select(img => img.ImageUrl)
                .ToList(),
            SellerId = listing.SellerId,
            SellerDisplayName = listing.Seller?.DisplayName ?? "未知賣家",
            CreatedAt = listing.CreatedAt,
            UpdatedAt = listing.UpdatedAt,
            CanMessage = canMessage,
            IsOwner = isOwner,
            SellerTotalCompletedTransactions = sellerTotalCompletedTransactions,
            SellerAverageRating = sellerAverageRating
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> My()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Challenge();
        }

        var listings = await _db.Listings
            .Include(l => l.Images)
            .Where(l => l.SellerId == user.Id)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        var listingItems = listings.Select(l => new ListingItem
        {
            Id = l.Id,
            Title = l.Title,
            Category = l.Category,
            Price = l.Price,
            IsFree = l.IsFree,
            IsCharity = l.IsCharity,
            Status = l.Status,
            FirstImageUrl = l.Images.OrderBy(img => img.SortOrder).FirstOrDefault()?.ImageUrl,
            CreatedAt = l.CreatedAt,
            UpdatedAt = l.UpdatedAt
        }).ToList();

        var viewModel = new MyListingsViewModel
        {
            Listings = listingItems
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Challenge();
        }

        var listing = await _db.Listings
            .Include(l => l.Images)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (listing == null)
        {
            return NotFound();
        }

        // 驗證是否為商品擁有者
        if (listing.SellerId != user.Id)
        {
            return Forbid();
        }

        // 只有刊登中的商品可以編輯
        if (listing.Status != ListingStatus.Active)
        {
            TempData["ErrorMessage"] = "只有刊登中的商品才能編輯";
            return RedirectToAction(nameof(Details), new { id });
        }

        var viewModel = new ListingEditViewModel
        {
            Id = listing.Id,
            Title = listing.Title,
            Description = listing.Description,
            Category = listing.Category,
            Condition = listing.Condition,
            Price = listing.Price,
            IsFree = listing.IsFree,
            IsCharity = listing.IsCharity,
            PickupLocation = listing.PickupLocation,
            ExistingImages = listing.Images
                .OrderBy(img => img.SortOrder)
                .Select(img => img.ImageUrl)
                .ToList()
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ListingEditViewModel model)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            // 重新載入現有圖片
            var listingForImages = await _db.Listings
                .Include(l => l.Images)
                .FirstOrDefaultAsync(l => l.Id == id);
            
            if (listingForImages != null)
            {
                model.ExistingImages = listingForImages.Images
                    .OrderBy(img => img.SortOrder)
                    .Select(img => img.ImageUrl)
                    .ToList();
            }
            
            return View(model);
        }

        // 使用服務層更新商品
        var result = await _listingService.UpdateListingAsync(id, model, user.Id);

        if (!result.Success)
        {
            if (result.ErrorMessage == "找不到商品")
            {
                return NotFound();
            }
            if (result.ErrorMessage == "無權限修改此商品")
            {
                return Forbid();
            }
            if (result.ErrorMessage?.Contains("只有刊登中的商品才能編輯") == true)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
                return RedirectToAction(nameof(Details), new { id });
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "更新商品時發生錯誤");
            
            // 重新載入現有圖片
            var listingForImages = await _db.Listings
                .Include(l => l.Images)
                .FirstOrDefaultAsync(l => l.Id == id);
            
            if (listingForImages != null)
            {
                model.ExistingImages = listingForImages.Images
                    .OrderBy(img => img.SortOrder)
                    .Select(img => img.ImageUrl)
                    .ToList();
            }
            
            return View(model);
        }

        return RedirectToAction("Details", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Challenge();
        }

        // 使用服務層刪除商品
        var result = await _listingService.DeleteListingAsync(id, user.Id);

        if (!result.Success)
        {
            if (result.ErrorMessage == "找不到商品")
            {
                return NotFound();
            }
            if (result.ErrorMessage == "無權限刪除此商品")
            {
                return Forbid();
            }
            if (result.ErrorMessage?.Contains("只有刊登中的商品才能刪除") == true)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["ErrorMessage"] = result.ErrorMessage ?? "刪除商品時發生錯誤，請稍後再試";
            return RedirectToAction("Details", new { id });
        }

        return RedirectToAction("My");
    }
}


