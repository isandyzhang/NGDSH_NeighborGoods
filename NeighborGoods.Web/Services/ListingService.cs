using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeighborGoods.Web.Constants;
using NeighborGoods.Web.Data;
using NeighborGoods.Web.Models.DTOs;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Models.ViewModels;
using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Services;

/// <summary>
/// 商品服務實作
/// </summary>
public class ListingService : IListingService
{
    private readonly AppDbContext _db;
    private readonly IBlobService _blobService;
    private readonly ILogger<ListingService> _logger;

    public ListingService(
        AppDbContext db,
        IBlobService blobService,
        ILogger<ListingService> logger)
    {
        _db = db;
        _blobService = blobService;
        _logger = logger;
    }

    public async Task<SearchResult<ListingIndexViewModel>> SearchListingsAsync(
        ListingSearchCriteria criteria,
        string? currentUserId,
        int page,
        int pageSize)
    {
        // 建立查詢基礎
        var query = _db.Listings
            .Where(l => l.Status == ListingStatus.Active);

        // 如果指定排除用戶，排除該用戶的商品
        if (!string.IsNullOrEmpty(criteria.ExcludeUserId))
        {
            query = query.Where(l => l.SellerId != criteria.ExcludeUserId);
        }

        // 關鍵字搜尋（標題或描述）
        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var searchTerm = criteria.SearchTerm.Trim();
            if (searchTerm.Length >= ListingConstants.MinSearchTermLength)
            {
                query = query.Where(l => l.Title.Contains(searchTerm) || l.Description.Contains(searchTerm));
            }
        }

        // 分類篩選
        if (criteria.Category.HasValue)
        {
            query = query.Where(l => l.Category == criteria.Category.Value);
        }

        // 新舊程度篩選
        if (criteria.Condition.HasValue)
        {
            query = query.Where(l => l.Condition == criteria.Condition.Value);
        }

        // 價格範圍篩選
        if (criteria.MinPrice.HasValue)
        {
            query = query.Where(l => l.Price >= criteria.MinPrice.Value);
        }

        if (criteria.MaxPrice.HasValue)
        {
            query = query.Where(l => l.Price <= criteria.MaxPrice.Value);
        }

        // 免費商品篩選
        if (criteria.IsFree == true)
        {
            query = query.Where(l => l.IsFree == true);
        }

        // 愛心商品篩選
        if (criteria.IsCharity == true)
        {
            query = query.Where(l => l.IsCharity == true);
        }

        // 計算總數
        var totalCount = await query.CountAsync();

        // 使用投影查詢，只選擇需要的欄位
        var viewModels = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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

        return new SearchResult<ListingIndexViewModel>
        {
            Items = viewModels,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ServiceResult<Guid>> CreateListingAsync(
        ListingCreateViewModel model,
        string userId)
    {
        try
        {
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
                return ServiceResult<Guid>.Fail("請至少上傳一張商品照片");
            }

            // 檢查刊登中商品數量限制
            var activeListingCount = await _db.Listings
                .CountAsync(l => l.SellerId == userId && l.Status == ListingStatus.Active);

            if (activeListingCount >= ListingConstants.MaxActiveListingsPerUser)
            {
                return ServiceResult<Guid>.Fail($"您目前已有 {ListingConstants.MaxActiveListingsPerUser} 個刊登中的商品，請先下架或售出部分商品後再刊登新商品");
            }

            // 處理免費邏輯
            var price = (int)model.Price;
            var isFree = model.IsFree;

            if (price > 0 && isFree)
            {
                // 如果使用者有輸入價格且勾選了免費商品，自動取消免費商品的勾選
                isFree = false;
            }
            else if (price == 0)
            {
                // 價格為 0 時自動設定為免費商品
                isFree = true;
            }
            else if (isFree && price > 0)
            {
                // 如果勾選免費索取但價格 > 0，價格強制為 0
                price = 0;
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
                    Price = price,
                    IsFree = isFree,
                    IsCharity = model.IsCharity,
                    Status = ListingStatus.Active,
                    SellerId = userId,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _db.Listings.Add(listing);
                await _db.SaveChangesAsync(); // 先儲存以取得 listing.Id

                // 處理圖片上傳（最多 5 張）
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
                    if (image.Length > FileUploadConstants.MaxFileSize)
                    {
                        _logger.LogWarning("圖片 {Index} 超過大小限制：{Size} bytes", i + 1, image.Length);
                        continue;
                    }

                    // 驗證檔案類型
                    if (!FileUploadConstants.AllowedContentTypes.Contains(image.ContentType.ToLowerInvariant()))
                    {
                        _logger.LogWarning("圖片 {Index} 格式不允許：{ContentType}", i + 1, image.ContentType);
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
                        // 繼續處理其他圖片，不中斷整個流程
                    }
                }

                // 驗證至少有一張圖片成功上傳
                if (successfulImageCount == 0)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<Guid>.Fail("至少需要成功上傳一張圖片，請檢查圖片格式和大小");
                }

                // 儲存圖片記錄
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return ServiceResult<Guid>.Ok(listing.Id);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "建立商品時發生錯誤");
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "建立商品時發生錯誤");
            return ServiceResult<Guid>.Fail("建立商品時發生錯誤，請稍後再試");
        }
    }

    public async Task<ServiceResult> UpdateListingAsync(
        Guid listingId,
        ListingEditViewModel model,
        string userId)
    {
        try
        {
            var listing = await _db.Listings
                .Include(l => l.Images)
                .FirstOrDefaultAsync(l => l.Id == listingId);

            if (listing == null)
            {
                return ServiceResult.Fail("找不到商品");
            }

            // 驗證是否為商品擁有者
            if (listing.SellerId != userId)
            {
                return ServiceResult.Fail("無權限修改此商品");
            }

            // 只有刊登中的商品可以編輯
            if (listing.Status != ListingStatus.Active)
            {
                return ServiceResult.Fail("只有刊登中的商品才能編輯");
            }

            // 處理免費邏輯
            var price = (int)model.Price;
            var isFree = model.IsFree;

            if (price == 0)
            {
                isFree = true;
            }
            else if (isFree)
            {
                price = 0;
            }

            // 更新商品欄位
            listing.Title = model.Title;
            listing.Description = model.Description;
            listing.Category = model.Category;
            listing.Condition = model.Condition;
            listing.Price = price;
            listing.IsFree = isFree;
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
            var availableSlots = FileUploadConstants.MaxImageCount - remainingImages;

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
                    if (image.Length > FileUploadConstants.MaxFileSize)
                    {
                        _logger.LogWarning("圖片 {Index} 超過大小限制：{Size} bytes", i + 1, image.Length);
                        continue;
                    }

                    // 驗證檔案類型
                    if (!FileUploadConstants.AllowedContentTypes.Contains(image.ContentType.ToLowerInvariant()))
                    {
                        _logger.LogWarning("圖片 {Index} 格式不允許：{ContentType}", i + 1, image.ContentType);
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

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新商品 {ListingId} 時發生錯誤", listingId);
            return ServiceResult.Fail("更新商品時發生錯誤，請稍後再試");
        }
    }

    public async Task<ServiceResult> DeleteListingAsync(
        Guid listingId,
        string userId)
    {
        try
        {
            var listing = await _db.Listings
                .Include(l => l.Images)
                .FirstOrDefaultAsync(l => l.Id == listingId);

            if (listing == null)
            {
                return ServiceResult.Fail("找不到商品");
            }

            // 驗證是否為商品擁有者
            if (listing.SellerId != userId)
            {
                return ServiceResult.Fail("無權限刪除此商品");
            }

            // 只有刊登中的商品可以刪除
            if (listing.Status != ListingStatus.Active)
            {
                return ServiceResult.Fail("只有刊登中的商品才能刪除");
            }

            // 刪除 Blob Storage 中的圖片
            if (listing.Images != null && listing.Images.Any())
            {
                var imageUrls = listing.Images.Select(img => img.ImageUrl).ToList();
                await _blobService.DeleteListingImagesAsync(imageUrls);
            }

            // 刪除資料庫記錄（ListingImage 會自動級聯刪除）
            _db.Listings.Remove(listing);
            await _db.SaveChangesAsync();

            _logger.LogInformation("用戶 {UserId} 刪除了商品 {ListingId}", userId, listingId);

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除商品 {ListingId} 時發生錯誤", listingId);
            return ServiceResult.Fail("刪除商品時發生錯誤，請稍後再試");
        }
    }

    public async Task<ServiceResult> DeactivateListingAsync(
        Guid listingId,
        string userId)
    {
        try
        {
            var listing = await _db.Listings
                .FirstOrDefaultAsync(l => l.Id == listingId);

            if (listing == null)
            {
                return ServiceResult.Fail("找不到商品");
            }

            // 驗證是否為商品擁有者
            if (listing.SellerId != userId)
            {
                return ServiceResult.Fail("無權限下架此商品");
            }

            // 只有刊登中的商品可以下架
            if (listing.Status != ListingStatus.Active)
            {
                return ServiceResult.Fail("只有刊登中的商品才能下架");
            }

            listing.Status = ListingStatus.Inactive;
            listing.UpdatedAt = TaiwanTime.Now;
            await _db.SaveChangesAsync();

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下架商品 {ListingId} 時發生錯誤", listingId);
            return ServiceResult.Fail("下架商品時發生錯誤，請稍後再試");
        }
    }
}

