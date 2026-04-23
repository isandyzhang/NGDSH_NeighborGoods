using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NeighborGoods.Api.Features.Listing.Contracts;
using NeighborGoods.Api.Features.PurchaseRequests;
using NeighborGoods.Api.Infrastructure.Storage;
using NeighborGoods.Api.Shared.Contracts;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Security;

namespace NeighborGoods.Api.Features.Listing.Services;

public sealed class ListingQueryService(
    NeighborGoodsDbContext dbContext,
    IMemoryCache memoryCache,
    IBlobStorage blobStorage,
    ICurrentUserContext currentUserContext)
{
    private static readonly TimeSpan LookupCacheDuration = TimeSpan.FromMinutes(5);
    private const string CategoryLookupCacheKey = "listing-lookups:categories";
    private const string ConditionLookupCacheKey = "listing-lookups:conditions";
    private const string ResidenceLookupCacheKey = "listing-lookups:residences";
    private const string PickupLocationLookupCacheKey = "listing-lookups:pickup-locations";

    public async Task<ListingDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var categoryNames = await GetCategoryMapAsync(cancellationToken);
        var conditionNames = await GetConditionMapAsync(cancellationToken);
        var residenceNames = await GetResidenceMapAsync(cancellationToken);
        var pickupNames = await GetPickupLocationMapAsync(cancellationToken);

        var listing = await dbContext.Listings
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (listing is null)
        {
            return null;
        }

        var storedImageUrls = await dbContext.ListingImages.AsNoTracking()
            .Where(x => x.ListingId == id)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .Select(x => x.ImageUrl)
            .ToListAsync(cancellationToken);
        var resolvedImageUrls = storedImageUrls
            .Select(ResolveImageUrl)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .ToList();
        var mainImageUrl = resolvedImageUrls.FirstOrDefault();
        var pendingSummary = await GetPendingPurchaseRequestSummaryAsync(id, now, cancellationToken);

        return new ListingDetailDto(
            listing.Id,
            listing.Title,
            listing.Description,
            listing.Category,
            categoryNames.GetValueOrDefault(listing.Category, "其他"),
            listing.Condition,
            conditionNames.GetValueOrDefault(listing.Condition, "良好"),
            Convert.ToInt32(listing.Price),
            listing.Residence,
            residenceNames.GetValueOrDefault(listing.Residence, "未指定"),
            listing.PickupLocation,
            pickupNames.GetValueOrDefault(listing.PickupLocation, "私訊"),
            mainImageUrl,
            resolvedImageUrls,
            listing.Status,
            listing.IsFree,
            listing.IsCharity,
            listing.IsTradeable,
            listing.IsPinned,
            listing.PinnedStartDate,
            listing.PinnedEndDate,
            pendingSummary?.ExpireAt,
            pendingSummary?.RemainingSeconds,
            listing.CreatedAt,
            listing.UpdatedAt);
    }

    public async Task<PagedResult<ListingListItemDto>> QueryAsync(ListingQueryRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var categoryNames = await GetCategoryMapAsync(cancellationToken);
        var conditionNames = await GetConditionMapAsync(cancellationToken);
        var residenceNames = await GetResidenceMapAsync(cancellationToken);

        var active = (int)ListingStatus.Active;
        var reserved = (int)ListingStatus.Reserved;

        var queryable = dbContext.Listings
            .Where(x => x.Status == active || x.Status == reserved);

        if (!string.IsNullOrWhiteSpace(request.ExcludeUserId))
        {
            queryable = queryable.Where(x => x.SellerId != request.ExcludeUserId);
        }

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var keyword = request.Query.Trim();
            if (keyword.Length >= ListingConstants.MinSearchTermLength)
            {
                queryable = queryable.Where(x =>
                    EF.Functions.Like(x.Title, $"%{keyword}%") ||
                    (x.Description != null && EF.Functions.Like(x.Description, $"%{keyword}%")));
            }
        }

        var categoryCodes = request.CategoryCodes?.Where(x => x > 0).Distinct().ToArray();
        if (categoryCodes is { Length: > 0 })
        {
            queryable = queryable.Where(x => categoryCodes.Contains(x.Category));
        }
        else if (request.CategoryCode is { } cat)
        {
            queryable = queryable.Where(x => x.Category == cat);
        }

        var conditionCodes = request.ConditionCodes?.Where(x => x > 0).Distinct().ToArray();
        if (conditionCodes is { Length: > 0 })
        {
            queryable = queryable.Where(x => conditionCodes.Contains(x.Condition));
        }
        else if (request.ConditionCode is { } cond)
        {
            queryable = queryable.Where(x => x.Condition == cond);
        }

        var residenceCodes = request.ResidenceCodes?.Where(x => x > 0).Distinct().ToArray();
        if (residenceCodes is { Length: > 0 })
        {
            queryable = queryable.Where(x => residenceCodes.Contains(x.Residence));
        }
        else if (request.ResidenceCode is { } res)
        {
            queryable = queryable.Where(x => x.Residence == res);
        }

        if (request.MinPrice is { } minP)
        {
            queryable = queryable.Where(x => x.Price >= minP);
        }

        if (request.MaxPrice is { } maxP)
        {
            queryable = queryable.Where(x => x.Price <= maxP);
        }

        if (request.IsCharity == true)
        {
            queryable = queryable.Where(x => x.IsCharity);
        }

        if (request.IsFree == true)
        {
            queryable = queryable.Where(x => x.IsFree);
        }

        if (request.IsTradeable == true)
        {
            queryable = queryable.Where(x => x.IsTradeable);
        }

        var todayUtc = DateTime.UtcNow.Date;
        queryable = queryable
            .OrderByDescending(x => x.IsPinned && x.PinnedEndDate.HasValue && x.PinnedEndDate.Value.Date >= todayUtc)
            .ThenByDescending(x => x.CreatedAt);

        var total = await queryable.CountAsync(cancellationToken);
        var listings = await queryable
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new ListingSummary(
                x.Id,
                x.SellerId,
                x.Title,
                x.Seller.DisplayName,
                x.Seller.EmailConfirmed,
                x.Seller.EmailNotificationEnabled,
                x.Seller.LineUserId != null,
                x.Seller.LineMessagingApiUserId != null,
                x.Seller.IsQuickResponder,
                x.Category,
                x.Condition,
                Convert.ToInt32(x.Price),
                x.Residence,
                x.Status,
                x.IsFree,
                x.IsCharity,
                x.IsTradeable,
                x.IsPinned,
                x.PinnedEndDate,
                null,
                null,
                dbContext.Conversations.Count(c => c.ListingId == x.Id)))
            .ToListAsync(cancellationToken);

        var coverImageMap = await GetCoverImageMapAsync(
            listings.Select(x => x.Id).ToList(),
            cancellationToken);
        var pendingMap = await GetPendingPurchaseRequestMapAsync(
            listings.Select(x => x.Id).ToList(),
            now,
            cancellationToken);

        var items = listings
            .Select(x =>
            {
                pendingMap.TryGetValue(x.Id, out var pendingSummary);
                return new ListingListItemDto(
                    x.Id,
                    x.SellerId,
                    x.Title,
                    x.SellerDisplayName,
                    x.SellerEmailVerified,
                    x.SellerEmailNotificationEnabled,
                    x.SellerLineLoginBound,
                    x.SellerLineNotifyBound,
                    x.SellerQuickResponder,
                    x.Category,
                    categoryNames.GetValueOrDefault(x.Category, "其他"),
                    x.Condition,
                    conditionNames.GetValueOrDefault(x.Condition, "良好"),
                    x.Price,
                    x.Residence,
                    residenceNames.GetValueOrDefault(x.Residence, "未指定"),
                    coverImageMap.GetValueOrDefault(x.Id),
                    x.Status,
                    x.IsFree,
                    x.IsCharity,
                    x.IsTradeable,
                    x.IsPinned,
                    x.PinnedEndDate,
                    pendingSummary?.ExpireAt,
                    pendingSummary?.RemainingSeconds,
                    x.InterestCount);
            })
            .ToList();

        return new PagedResult<ListingListItemDto>(page, pageSize, total, items);
    }

    public async Task<PagedResult<MyListingListItemDto>> QueryMineAsync(ListingQueryRequest request, CancellationToken cancellationToken = default)
    {
        var sellerId = currentUserContext.GetRequiredUserId();
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var categoryNames = await GetCategoryMapAsync(cancellationToken);

        var queryable = dbContext.Listings
            .Where(x => x.SellerId == sellerId)
            .OrderByDescending(x => x.CreatedAt);

        var total = await queryable.CountAsync(cancellationToken);
        var rows = await queryable
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Category,
                x.Price,
                x.IsFree,
                x.IsCharity,
                x.IsTradeable,
                x.Status,
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(r => r.Id).ToList();
        var coverImageMap = await GetCoverImageMapAsync(ids, cancellationToken);

        var items = rows
            .Select(x => new MyListingListItemDto(
                x.Id,
                x.Title,
                x.Category,
                categoryNames.GetValueOrDefault(x.Category, "其他"),
                Convert.ToInt32(x.Price),
                x.IsFree,
                x.IsCharity,
                x.IsTradeable,
                x.Status,
                coverImageMap.GetValueOrDefault(x.Id),
                x.CreatedAt,
                x.UpdatedAt))
            .ToList();

        return new PagedResult<MyListingListItemDto>(page, pageSize, total, items);
    }

    public async Task<SellerListingsQueryResultDto?> QueryBySellerAsync(
        string sellerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sellerId))
        {
            return null;
        }

        var seller = await dbContext.AspNetUsers
            .AsNoTracking()
            .Where(x => x.Id == sellerId)
            .Select(x => new { x.Id, x.DisplayName })
            .FirstOrDefaultAsync(cancellationToken);
        if (seller is null)
        {
            return null;
        }

        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (normalizedPage - 1) * normalizedPageSize;
        var active = (int)ListingStatus.Active;
        var reserved = (int)ListingStatus.Reserved;
        var sold = (int)ListingStatus.Sold;
        var donated = (int)ListingStatus.Donated;
        var givenOrTraded = (int)ListingStatus.GivenOrTraded;

        var visibleStatuses = new[] { active, reserved, sold, donated, givenOrTraded };
        var queryable = dbContext.Listings
            .AsNoTracking()
            .Where(x => x.SellerId == sellerId && visibleStatuses.Contains(x.Status))
            .OrderByDescending(x => x.CreatedAt);

        var total = await queryable.CountAsync(cancellationToken);
        var rows = await queryable
            .Skip(skip)
            .Take(normalizedPageSize)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Category,
                x.Price,
                x.IsFree,
                x.Status,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var categoryNames = await GetCategoryMapAsync(cancellationToken);
        var coverImageMap = await GetCoverImageMapAsync(rows.Select(x => x.Id).ToList(), cancellationToken);

        var items = rows
            .Select(x => new SellerListingListItemDto(
                x.Id,
                x.Title,
                x.Category,
                categoryNames.GetValueOrDefault(x.Category, "其他"),
                Convert.ToInt32(x.Price),
                x.IsFree,
                x.Status,
                coverImageMap.GetValueOrDefault(x.Id),
                x.CreatedAt))
            .ToList();

        var totalListings = await dbContext.Listings
            .AsNoTracking()
            .CountAsync(x => x.SellerId == sellerId, cancellationToken);
        var activeListings = await dbContext.Listings
            .AsNoTracking()
            .CountAsync(x => x.SellerId == sellerId && x.Status == active, cancellationToken);
        var completedListings = await dbContext.Listings
            .AsNoTracking()
            .CountAsync(
                x => x.SellerId == sellerId &&
                     (x.Status == sold || x.Status == donated || x.Status == givenOrTraded),
                cancellationToken);

        var sellerSummary = new SellerSummaryDto(
            seller.Id,
            string.IsNullOrWhiteSpace(seller.DisplayName) ? "賣家" : seller.DisplayName,
            totalListings,
            activeListings,
            completedListings);

        return new SellerListingsQueryResultDto(
            sellerSummary,
            items,
            normalizedPage,
            normalizedPageSize,
            total);
    }

    private async Task<Dictionary<int, string>> GetCategoryMapAsync(CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue<Dictionary<int, string>>(CategoryLookupCacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var map = await dbContext.ListingCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToDictionaryAsync(c => c.Id, c => c.DisplayName, cancellationToken);

        memoryCache.Set(CategoryLookupCacheKey, map, LookupCacheDuration);
        return map;
    }

    private async Task<Dictionary<int, string>> GetConditionMapAsync(CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue<Dictionary<int, string>>(ConditionLookupCacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var map = await dbContext.ListingConditions.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToDictionaryAsync(c => c.Id, c => c.DisplayName, cancellationToken);

        memoryCache.Set(ConditionLookupCacheKey, map, LookupCacheDuration);
        return map;
    }

    private async Task<Dictionary<int, string>> GetResidenceMapAsync(CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue<Dictionary<int, string>>(ResidenceLookupCacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var map = await dbContext.ListingResidences.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToDictionaryAsync(c => c.Id, c => c.DisplayName, cancellationToken);

        memoryCache.Set(ResidenceLookupCacheKey, map, LookupCacheDuration);
        return map;
    }

    private async Task<Dictionary<int, string>> GetPickupLocationMapAsync(CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue<Dictionary<int, string>>(PickupLocationLookupCacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var map = await dbContext.ListingPickupLocations.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToDictionaryAsync(c => c.Id, c => c.DisplayName, cancellationToken);

        memoryCache.Set(PickupLocationLookupCacheKey, map, LookupCacheDuration);
        return map;
    }

    private async Task<Dictionary<Guid, string?>> GetCoverImageMapAsync(
        IReadOnlyCollection<Guid> listingIds,
        CancellationToken cancellationToken)
    {
        if (listingIds.Count == 0)
        {
            return [];
        }

        var images = await dbContext.ListingImages.AsNoTracking()
            .Where(x => listingIds.Contains(x.ListingId))
            .OrderBy(x => x.ListingId)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .Select(x => new
            {
                x.ListingId,
                x.ImageUrl
            })
            .ToListAsync(cancellationToken);

        return images
            .GroupBy(x => x.ListingId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var raw = g.FirstOrDefault()?.ImageUrl;
                    return string.IsNullOrWhiteSpace(raw) ? null : ResolveImageUrl(raw);
                });
    }

    private string ResolveImageUrl(string storedPathOrUrl)
    {
        if (storedPathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            storedPathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return storedPathOrUrl;
        }

        return blobStorage.BuildPublicUrl(storedPathOrUrl);
    }

    private async Task<PendingPurchaseRequestSummary?> GetPendingPurchaseRequestSummaryAsync(
        Guid listingId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var pendingStatus = (int)PurchaseRequestStatus.Pending;
        var pendingExpireAt = await dbContext.PurchaseRequests
            .AsNoTracking()
            .Where(x => x.ListingId == listingId && x.Status == pendingStatus && x.ExpireAt > now)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.ExpireAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (pendingExpireAt == default)
        {
            return null;
        }

        return new PendingPurchaseRequestSummary(
            pendingExpireAt,
            ToRemainingSeconds(pendingExpireAt, now));
    }

    private async Task<Dictionary<Guid, PendingPurchaseRequestSummary>> GetPendingPurchaseRequestMapAsync(
        IReadOnlyCollection<Guid> listingIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (listingIds.Count == 0)
        {
            return [];
        }

        var pendingStatus = (int)PurchaseRequestStatus.Pending;
        var rows = await dbContext.PurchaseRequests
            .AsNoTracking()
            .Where(x => listingIds.Contains(x.ListingId) && x.Status == pendingStatus && x.ExpireAt > now)
            .GroupBy(x => x.ListingId)
            .Select(g => new
            {
                ListingId = g.Key,
                ExpireAt = g.Min(x => x.ExpireAt)
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            x => x.ListingId,
            x => new PendingPurchaseRequestSummary(
                x.ExpireAt,
                ToRemainingSeconds(x.ExpireAt, now)));
    }

    private static int ToRemainingSeconds(DateTime expireAt, DateTime now)
    {
        if (expireAt <= now)
        {
            return 0;
        }

        return Math.Max(0, (int)Math.Ceiling((expireAt - now).TotalSeconds));
    }

    private sealed record PendingPurchaseRequestSummary(DateTime ExpireAt, int RemainingSeconds);
}
