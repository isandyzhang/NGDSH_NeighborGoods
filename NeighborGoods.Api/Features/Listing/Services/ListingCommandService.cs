using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Listing;
using NeighborGoods.Api.Features.Listing.Contracts;
using NeighborGoods.Api.Infrastructure.Storage;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;
using NeighborGoods.Api.Shared.Security;

namespace NeighborGoods.Api.Features.Listing.Services;

public sealed class ListingCommandService(
    NeighborGoodsDbContext dbContext,
    ICurrentUserContext currentUserContext,
    IBlobStorage blobStorage,
    ILogger<ListingCommandService> logger)
{
    /// <summary>建立商品並上傳至少一張圖（單一交易）；失敗時 best-effort 刪除已上傳的 Blob。</summary>
    public async Task<Guid> CreateWithImagesAsync(
        CreateListingRequest request,
        IReadOnlyList<IFormFile> imageFiles,
        CancellationToken cancellationToken = default)
    {
        if (imageFiles.Count == 0)
        {
            throw new ArgumentException("至少需要上傳一張商品照片。", nameof(imageFiles));
        }

        if (imageFiles.Count > ListingImageUploadRules.MaxImageCount)
        {
            throw new ArgumentException(
                $"Image count exceeds maximum allowed ({ListingImageUploadRules.MaxImageCount}).",
                nameof(imageFiles));
        }

        foreach (var file in imageFiles)
        {
            ValidateImageFile(file);
        }

        var (listing, _) = await PrepareListingForCreateAsync(request, cancellationToken);

        var uploadedBlobNames = new List<string>();
        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.Listings.AddAsync(listing, cancellationToken);
            var sortOrder = 0;
            foreach (var file in imageFiles)
            {
                var blobName = $"listings/{listing.Id}/{sortOrder}-{Guid.NewGuid()}.jpg";
                await using (var stream = file.OpenReadStream())
                {
                    await blobStorage.UploadCompressedJpegAsync(blobName, stream, cancellationToken);
                }

                uploadedBlobNames.Add(blobName);
                await dbContext.ListingImages.AddAsync(
                    new ListingImage
                    {
                        Id = Guid.NewGuid(),
                        ListingId = listing.Id,
                        ImageUrl = blobName,
                        SortOrder = sortOrder,
                        CreatedAt = DateTime.UtcNow
                    },
                    cancellationToken);
                sortOrder++;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "建立商品失敗（交易將回滾）：Seller={SellerId}, ListingId={ListingId}, ImageCount={ImageCount}",
                listing.SellerId,
                listing.Id,
                imageFiles.Count);

            try
            {
                await tx.RollbackAsync(cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                logger.LogError(rollbackEx, "建立商品回滾交易失敗：ListingId={ListingId}", listing.Id);
            }

            await TryDeleteBlobsAsync(uploadedBlobNames, cancellationToken);
            throw;
        }

        return listing.Id;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateListingRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.Title, request.Price);
        await EnsureActiveCategoryExistsAsync(request.CategoryCode, cancellationToken);
        await EnsureActiveConditionExistsAsync(request.ConditionCode, cancellationToken);
        await EnsureActiveResidenceExistsAsync(request.ResidenceCode, cancellationToken);
        await EnsureActivePickupLocationExistsAsync(request.PickupLocationCode, cancellationToken);

        var entity = await dbContext.Listings
            .Include(x => x.ListingImages)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        EnsureCurrentUserOwnsListing(entity);

        var status = (ListingStatus)entity.Status;
        if (status is not (ListingStatus.Active or ListingStatus.Inactive))
        {
            throw new ListingAccessException(
                "LISTING_NOT_EDITABLE",
                "只有刊登中或已下架的商品才能編輯",
                StatusCodes.Status403Forbidden);
        }

        var (price, isFree) = NormalizePriceAndFreeForUpdate(request.Price, request.IsFree);

        entity.Title = request.Title.Trim();
        entity.Description = request.Description?.Trim() ?? string.Empty;
        entity.Category = request.CategoryCode;
        entity.Condition = request.ConditionCode;
        entity.Price = price;
        entity.Residence = request.ResidenceCode;
        entity.PickupLocation = request.PickupLocationCode;
        entity.IsFree = isFree;
        entity.IsCharity = request.IsCharity;
        entity.IsTradeable = request.IsTradeable;
        entity.UpdatedAt = DateTime.UtcNow;

        if (request.ImageUrlsToDelete is { Count: > 0 } toDelete)
        {
            foreach (var token in toDelete)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                var trimmed = token.Trim();
                var images = entity.ListingImages
                    .Where(img =>
                        ListingBlobPath.StoredImageMatchesDeleteToken(
                            img.ImageUrl,
                            trimmed,
                            raw => ResolveImageUrlForMatch(raw)))
                    .ToList();
                foreach (var img in images)
                {
                    try
                    {
                        var blobName = ListingBlobPath.ToBlobNameForDeletion(img.ImageUrl);
                        if (!string.IsNullOrEmpty(blobName))
                        {
                            await blobStorage.DeleteAsync(blobName, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "刪除商品圖片 Blob 失敗：ListingId={ListingId}, ImageUrl={ImageUrl}", id, img.ImageUrl);
                    }

                    dbContext.ListingImages.Remove(img);
                    entity.ListingImages.Remove(img);
                }
            }

        }

        var orderedImages = BuildOrderedImages(
            entity.ListingImages.ToList(),
            request.ImageUrlsInOrder);
        for (var i = 0; i < orderedImages.Count; i++)
        {
            orderedImages[i].SortOrder = i;
        }

        dbContext.Listings.Update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Listings
            .Include(x => x.ListingImages)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        EnsureCurrentUserOwnsListing(entity);

        var status = (ListingStatus)entity.Status;
        if (status is not (ListingStatus.Active or ListingStatus.Inactive))
        {
            throw new ListingAccessException(
                "LISTING_DELETE_NOT_ALLOWED",
                "只有刊登中或已下架的商品才能刪除",
                StatusCodes.Status409Conflict);
        }

        var imageUrls = entity.ListingImages.Select(img => img.ImageUrl).ToList();
        dbContext.Listings.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var url in imageUrls)
        {
            try
            {
                var blobName = ListingBlobPath.ToBlobNameForDeletion(url);
                if (!string.IsNullOrEmpty(blobName))
                {
                    await blobStorage.DeleteAsync(blobName, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "刪除商品後清理 Blob 失敗：ListingId={ListingId}, ImageUrl={ImageUrl}", id, url);
            }
        }

        return true;
    }

    public async Task<ListingImageUploadResult> AddImageAsync(
        Guid listingId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        ValidateImageFile(file);

        var listing = await dbContext.Listings
            .FirstOrDefaultAsync(x => x.Id == listingId, cancellationToken);
        if (listing is null)
        {
            throw new ListingAccessException("LISTING_NOT_FOUND", "找不到商品", StatusCodes.Status404NotFound);
        }

        EnsureCurrentUserOwnsListing(listing);

        var existingImageCount = await dbContext.ListingImages
            .CountAsync(x => x.ListingId == listingId, cancellationToken);
        if (existingImageCount >= ListingImageUploadRules.MaxImageCount)
        {
            throw new ArgumentException("Image count exceeds maximum allowed (5).", nameof(file));
        }

        var nextSortOrder = await dbContext.ListingImages
            .Where(x => x.ListingId == listingId)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(cancellationToken) ?? -1;
        nextSortOrder += 1;

        var blobName = $"listings/{listingId}/{nextSortOrder}-{Guid.NewGuid()}.jpg";
        await using var stream = file.OpenReadStream();
        await blobStorage.UploadCompressedJpegAsync(blobName, stream, cancellationToken);

        var imageEntity = new ListingImage
        {
            Id = Guid.NewGuid(),
            ListingId = listingId,
            ImageUrl = blobName,
            SortOrder = nextSortOrder,
            CreatedAt = DateTime.UtcNow
        };

        await dbContext.ListingImages.AddAsync(imageEntity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ListingImageUploadResult(imageEntity.Id, imageEntity.SortOrder, blobName);
    }

    private async Task<(Listing Listing, AspNetUser Seller)> PrepareListingForCreateAsync(
        CreateListingRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request.Title, request.Price);
        await EnsureActiveCategoryExistsAsync(request.CategoryCode, cancellationToken);
        await EnsureActiveConditionExistsAsync(request.ConditionCode, cancellationToken);
        await EnsureActiveResidenceExistsAsync(request.ResidenceCode, cancellationToken);
        await EnsureActivePickupLocationExistsAsync(request.PickupLocationCode, cancellationToken);
        var sellerId = currentUserContext.GetRequiredUserId();
        var seller = await dbContext.AspNetUsers
            .FirstOrDefaultAsync(x => x.Id == sellerId, cancellationToken);
        if (seller is null)
        {
            throw new ListingAccessException("AUTH_USER_NOT_FOUND", "找不到登入使用者", StatusCodes.Status401Unauthorized);
        }

        if (!seller.EmailConfirmed)
        {
            throw new ListingAccessException("EMAIL_NOT_CONFIRMED", "請先完成 Email 驗證後再上架", StatusCodes.Status403Forbidden);
        }

        if (!seller.EmailNotificationEnabled)
        {
            throw new ListingAccessException(
                "LISTING_EMAIL_NOTIFICATION_REQUIRED",
                "刊登商品前，請先於個人資料頁面開啟通知。",
                StatusCodes.Status403Forbidden);
        }

        var active = (int)ListingStatus.Active;
        var activeListingCount = await dbContext.Listings
            .CountAsync(l => l.SellerId == sellerId && l.Status == active, cancellationToken);
        if (activeListingCount >= ListingConstants.MaxActiveListingsPerUser)
        {
            throw new ListingAccessException(
                "LISTING_MAX_ACTIVE_REACHED",
                $"您目前已有 {ListingConstants.MaxActiveListingsPerUser} 個刊登中的商品，請先下架或售出部分商品後再刊登新商品",
                StatusCodes.Status409Conflict);
        }

        var (price, isFree) = NormalizePriceAndFreeForCreate(request.Price, request.IsFree);

        if (request.UseTopPin && seller.TopPinCredits <= 0)
        {
            throw new ListingAccessException("LISTING_TOP_PIN_NO_CREDITS", "您沒有可用的置頂次數", StatusCodes.Status400BadRequest);
        }

        var now = DateTime.UtcNow;
        var entity = new Listing
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Price = price,
            IsFree = isFree,
            IsCharity = request.IsCharity,
            SellerId = sellerId,
            Category = request.CategoryCode,
            PickupLocation = request.PickupLocationCode,
            Condition = request.ConditionCode,
            BuyerId = null,
            Residence = request.ResidenceCode,
            IsTradeable = request.IsTradeable,
            IsPinned = false,
            Status = active,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (request.UseTopPin)
        {
            seller.TopPinCredits -= 1;
            entity.IsPinned = true;
            entity.PinnedStartDate = now;
            entity.PinnedEndDate = now.AddDays(7);
        }

        return (entity, seller);
    }

    private async Task TryDeleteBlobsAsync(IReadOnlyList<string> blobNames, CancellationToken cancellationToken)
    {
        foreach (var name in blobNames)
        {
            try
            {
                await blobStorage.DeleteAsync(name, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "建立失敗後清理 Blob：{BlobName}", name);
            }
        }
    }

    private static void ValidateImageFile(IFormFile file)
    {
        if (file.Length <= 0)
        {
            throw new ArgumentException("Image file is required.", nameof(file));
        }

        if (file.Length > ListingImageUploadRules.MaxFileSize)
        {
            throw new ArgumentException("Image file size exceeds 5MB limit.", nameof(file));
        }

        var contentType = file.ContentType?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(contentType) || !ListingImageUploadRules.AllowedContentTypes.Contains(contentType))
        {
            throw new ArgumentException("Unsupported image content type.", nameof(file));
        }
    }

    private string ResolveImageUrlForMatch(string storedRaw) =>
        storedRaw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        storedRaw.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? storedRaw
            : blobStorage.BuildPublicUrl(storedRaw);

    private static (decimal Price, bool IsFree) NormalizePriceAndFreeForCreate(int requestPrice, bool requestIsFree)
    {
        var price = requestPrice;
        var isFree = requestIsFree;
        if (price > 0 && isFree)
        {
            isFree = false;
        }
        else if (price == 0)
        {
            isFree = true;
        }
        else if (isFree && price > 0)
        {
            price = 0;
        }

        return (price, isFree);
    }

    private static (decimal Price, bool IsFree) NormalizePriceAndFreeForUpdate(int requestPrice, bool requestIsFree)
    {
        var price = requestPrice;
        var isFree = requestIsFree;
        if (price == 0)
        {
            isFree = true;
        }
        else if (isFree)
        {
            price = 0;
        }

        return (price, isFree);
    }

    private async Task EnsureActiveCategoryExistsAsync(int categoryCode, CancellationToken cancellationToken)
    {
        var exists = await dbContext.ListingCategories.AnyAsync(
            c => c.Id == categoryCode && c.IsActive,
            cancellationToken);
        if (!exists)
        {
            throw new ArgumentException("Invalid or inactive category.", nameof(categoryCode));
        }
    }

    private async Task EnsureActiveConditionExistsAsync(int conditionCode, CancellationToken cancellationToken)
    {
        var exists = await dbContext.ListingConditions.AnyAsync(
            c => c.Id == conditionCode && c.IsActive,
            cancellationToken);
        if (!exists)
        {
            throw new ArgumentException("Invalid or inactive condition.", nameof(conditionCode));
        }
    }

    private async Task EnsureActiveResidenceExistsAsync(int residenceCode, CancellationToken cancellationToken)
    {
        var exists = await dbContext.ListingResidences.AnyAsync(
            c => c.Id == residenceCode && c.IsActive,
            cancellationToken);
        if (!exists)
        {
            throw new ArgumentException("Invalid or inactive residence.", nameof(residenceCode));
        }
    }

    private async Task EnsureActivePickupLocationExistsAsync(int pickupLocationCode, CancellationToken cancellationToken)
    {
        var exists = await dbContext.ListingPickupLocations.AnyAsync(
            c => c.Id == pickupLocationCode && c.IsActive,
            cancellationToken);
        if (!exists)
        {
            throw new ArgumentException("Invalid or inactive pickup location.", nameof(pickupLocationCode));
        }
    }

    private void EnsureCurrentUserOwnsListing(Listing entity)
    {
        var userId = currentUserContext.GetRequiredUserId();
        if (!string.Equals(entity.SellerId, userId, StringComparison.Ordinal))
        {
            throw new ListingAccessException(
                "LISTING_ACCESS_DENIED",
                "僅賣家本人可操作此商品",
                StatusCodes.Status403Forbidden);
        }
    }

    private static void ValidateRequest(string title, int price)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        if (price < 0)
        {
            throw new ArgumentException("Price cannot be negative.", nameof(price));
        }
    }

    private List<ListingImage> BuildOrderedImages(
        List<ListingImage> currentImages,
        IReadOnlyList<string>? orderTokens)
    {
        if (currentImages.Count == 0)
        {
            return currentImages;
        }

        if (orderTokens is not { Count: > 0 })
        {
            return currentImages
                .OrderBy(img => img.SortOrder)
                .ThenBy(img => img.CreatedAt)
                .ToList();
        }

        var normalizedTokens = orderTokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.Trim())
            .ToList();
        if (normalizedTokens.Count == 0)
        {
            return currentImages
                .OrderBy(img => img.SortOrder)
                .ThenBy(img => img.CreatedAt)
                .ToList();
        }

        var remaining = currentImages.ToList();
        var ordered = new List<ListingImage>(currentImages.Count);
        foreach (var token in normalizedTokens)
        {
            var match = remaining.FirstOrDefault(img =>
                ListingBlobPath.StoredImageMatchesDeleteToken(
                    img.ImageUrl,
                    token,
                    raw => ResolveImageUrlForMatch(raw)));
            if (match is null)
            {
                continue;
            }

            ordered.Add(match);
            remaining.Remove(match);
        }

        if (remaining.Count > 0)
        {
            ordered.AddRange(
                remaining
                    .OrderBy(img => img.SortOrder)
                    .ThenBy(img => img.CreatedAt));
        }

        return ordered;
    }
}
