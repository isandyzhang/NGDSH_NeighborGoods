using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Web.Data;
using NeighborGoods.Web.Infrastructure;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Models.ViewModels;
using NeighborGoods.Web.Services;
using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Controllers;

[Authorize]
public class ListingController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IBlobService _blobService;
    private readonly ILogger<ListingController> _logger;

    private const long MaxFileSize = 5 * 1024 * 1024; // 5MB
    private static readonly string[] AllowedContentTypes = { 
        "image/jpeg", 
        "image/jpg", 
        "image/png", 
        "image/gif", 
        "image/webp",
        "image/heic",
        "image/heif",
        "image/heic-sequence"
    };

    public ListingController(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IBlobService blobService,
        ILogger<ListingController> logger)
        : base(userManager)
    {
        _db = db;
        _blobService = blobService;
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

        // 驗證至少需要上傳一張圖片
        var images = new[]
        {
            model.Image1,
            model.Image2,
            model.Image3,
            model.Image4,
            model.Image5
        };

        var hasAnyImage = images.Any(img => img != null && img.Length > 0);
        if (!hasAnyImage)
        {
            ModelState.AddModelError(string.Empty, "請至少上傳一張商品照片");
            return View(model);
        }

        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Challenge(); // 要求重新登入
        }

        // 檢查刊登中商品數量限制（每帳號最多同時 10 個刊登中商品）
        var activeListingCount = await _db.Listings
            .CountAsync(l => l.SellerId == user.Id && 
                             l.Status == ListingStatus.Active);

        if (activeListingCount >= 10)
        {
            ModelState.AddModelError(string.Empty, "您目前已有 10 個刊登中的商品，請先下架或售出部分商品後再刊登新商品");
            return View(model);
        }

        // 處理免費邏輯
        if (model.Price > 0 && model.IsFree)
        {
            // 如果使用者有輸入價格且勾選了免費商品，自動取消免費商品的勾選
            model.IsFree = false;
        }
        else if (model.Price == 0)
        {
            // 價格為 0 時自動設定為免費商品
            model.IsFree = true;
        }
        else if (model.IsFree && model.Price > 0)
        {
            // 如果勾選免費索取但價格 > 0，價格強制為 0（這個情況理論上不會發生，因為前端已經處理）
            model.Price = 0;
        }

        var now = TaiwanTime.Now;

        // 使用資料庫交易確保資料一致性
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var listing = new Listing
            {
                Id = Guid.NewGuid(),
                Title = model.Title,
                Description = model.Description,
                Category = model.Category,
                Condition = model.Condition,
                PickupLocation = model.PickupLocation,
                Price = model.Price,
                IsFree = model.IsFree,
                IsCharity = model.IsCharity,
                Status = ListingStatus.Active,
                SellerId = user.Id,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.Listings.Add(listing);
            await _db.SaveChangesAsync(); // 先儲存以取得 listing.Id

            // 處理圖片上傳（最多 5 張）
            images = new[]
            {
                model.Image1,
                model.Image2,
                model.Image3,
                model.Image4,
                model.Image5
            };

            var successfulImageCount = 0;
            var imageSortOrder = 0;

            for (var i = 0; i < images.Length; i++)
            {
                var image = images[i];
                if (image == null || image.Length == 0)
                {
                    continue; // 跳過未上傳的圖片
                }

                // 驗證檔案大小
                if (image.Length > MaxFileSize)
                {
                    _logger.LogWarning("圖片 {Index} 超過大小限制：{Size} bytes", i + 1, image.Length);
                    ModelState.AddModelError($"Image{i + 1}", $"圖片大小不能超過 {MaxFileSize / 1024 / 1024}MB");
                    continue;
                }

                // 驗證檔案類型
                if (!AllowedContentTypes.Contains(image.ContentType.ToLowerInvariant()))
                {
                    _logger.LogWarning("圖片 {Index} 格式不允許：{ContentType}", i + 1, image.ContentType);
                    ModelState.AddModelError($"Image{i + 1}", "只允許上傳 jpg, jpeg, png, gif, webp, heic, heif 格式的圖片");
                    continue;
                }

                try
                {
                    // 上傳圖片到 Blob Storage
                    var blobUrl = await _blobService.UploadListingImageAsync(
                        listing.Id,
                        image.OpenReadStream(),
                        image.ContentType,
                        imageSortOrder);

                    // 建立圖片記錄
                    _db.ListingImages.Add(new ListingImage
                    {
                        Id = Guid.NewGuid(),
                        ListingId = listing.Id,
                        ImageUrl = blobUrl,
                        SortOrder = imageSortOrder,
                        CreatedAt = now
                    });

                    successfulImageCount++;
                    imageSortOrder++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "上傳圖片 {Index} 時發生錯誤", i + 1);
                    ModelState.AddModelError($"Image{i + 1}", "圖片上傳失敗，請稍後再試");
                    // 繼續處理其他圖片，不中斷整個流程
                }
            }

            // 驗證至少有一張圖片成功上傳
            if (successfulImageCount == 0)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "至少需要成功上傳一張圖片，請檢查圖片格式和大小");
                return View(model);
            }

            // 儲存圖片記錄
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "建立商品時發生錯誤");
            ModelState.AddModelError(string.Empty, "建立商品時發生錯誤，請稍後再試");
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

        // 處理免費邏輯
        if (model.Price == 0)
        {
            model.IsFree = true;
        }
        else if (model.IsFree)
        {
            model.Price = 0;
        }

        // 更新商品欄位
        listing.Title = model.Title;
        listing.Description = model.Description;
        listing.Category = model.Category;
        listing.Condition = model.Condition;
        listing.Price = model.Price;
        listing.IsFree = model.IsFree;
        listing.IsCharity = model.IsCharity;
        listing.PickupLocation = model.PickupLocation;
        listing.UpdatedAt = TaiwanTime.Now;

        // 處理要刪除的圖片
        if (model.ImagesToDelete != null && model.ImagesToDelete.Any())
        {
            var imagesToDelete = listing.Images
                .Where(img => model.ImagesToDelete.Contains(img.ImageUrl))
                .ToList();

            foreach (var image in imagesToDelete)
            {
                try
                {
                    await _blobService.DeleteListingImagesAsync(new List<string> { image.ImageUrl });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "刪除圖片 {ImageUrl} 時發生錯誤", image.ImageUrl);
                }

                _db.ListingImages.Remove(image);
            }
        }

        // 處理新上傳的圖片
        var newImages = new[]
        {
            model.Image1,
            model.Image2,
            model.Image3,
            model.Image4,
            model.Image5
        };

        // 計算現有圖片數量（刪除後）
        var remainingImages = listing.Images.Count - (model.ImagesToDelete?.Count ?? 0);
        var availableSlots = 5 - remainingImages;

        if (availableSlots > 0)
        {
            var sortOrderOffset = remainingImages;

            for (var i = 0; i < newImages.Length && i < availableSlots; i++)
            {
                var image = newImages[i];
                if (image == null || image.Length == 0)
                {
                    continue;
                }

                // 驗證檔案大小
                if (image.Length > MaxFileSize)
                {
                    _logger.LogWarning("圖片 {Index} 超過大小限制：{Size} bytes", i + 1, image.Length);
                    ModelState.AddModelError($"Image{i + 1}", $"圖片大小不能超過 {MaxFileSize / 1024 / 1024}MB");
                    continue;
                }

                // 驗證檔案類型
                if (!AllowedContentTypes.Contains(image.ContentType.ToLowerInvariant()))
                {
                    _logger.LogWarning("圖片 {Index} 格式不允許：{ContentType}", i + 1, image.ContentType);
                    ModelState.AddModelError($"Image{i + 1}", "只允許上傳 jpg, jpeg, png, gif, webp, heic, heif 格式的圖片");
                    continue;
                }

                try
                {
                    // 上傳圖片到 Blob Storage
                    var blobUrl = await _blobService.UploadListingImageAsync(
                        listing.Id,
                        image.OpenReadStream(),
                        image.ContentType,
                        sortOrderOffset + i);

                    // 建立圖片記錄
                    _db.ListingImages.Add(new ListingImage
                    {
                        Id = Guid.NewGuid(),
                        ListingId = listing.Id,
                        ImageUrl = blobUrl,
                        SortOrder = sortOrderOffset + i,
                        CreatedAt = TaiwanTime.Now
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "上傳圖片 {Index} 時發生錯誤", i + 1);
                    ModelState.AddModelError($"Image{i + 1}", "圖片上傳失敗，請稍後再試");
                }
            }
        }

        // 重新排序現有圖片（確保 SortOrder 連續）
        var remainingImagesList = listing.Images
            .Where(img => model.ImagesToDelete == null || !model.ImagesToDelete.Contains(img.ImageUrl))
            .OrderBy(img => img.SortOrder)
            .ToList();

        for (var i = 0; i < remainingImagesList.Count; i++)
        {
            remainingImagesList[i].SortOrder = i;
        }

        await _db.SaveChangesAsync();

        return RedirectToAction("Details", new { id = listing.Id });
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

        // 只有刊登中的商品可以刪除
        if (listing.Status != ListingStatus.Active)
        {
            TempData["ErrorMessage"] = "只有刊登中的商品才能刪除";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            // 刪除 Blob Storage 中的圖片
            if (listing.Images != null && listing.Images.Any())
            {
                var imageUrls = listing.Images.Select(img => img.ImageUrl).ToList();
                await _blobService.DeleteListingImagesAsync(imageUrls);
            }

            // 刪除資料庫記錄（ListingImage 會自動級聯刪除）
            _db.Listings.Remove(listing);
            await _db.SaveChangesAsync();

            _logger.LogInformation("用戶 {UserId} 刪除了商品 {ListingId}", user.Id, listing.Id);

            return RedirectToAction("My");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除商品 {ListingId} 時發生錯誤", listing.Id);
            ModelState.AddModelError(string.Empty, "刪除商品時發生錯誤，請稍後再試");
            return RedirectToAction("Details", new { id = listing.Id });
        }
    }
}


