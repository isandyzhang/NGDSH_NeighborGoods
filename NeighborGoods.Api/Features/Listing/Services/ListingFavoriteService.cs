using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Listing.Contracts;
using NeighborGoods.Api.Infrastructure.Storage;
using NeighborGoods.Api.Shared.Contracts;
using NeighborGoods.Api.Shared.Persistence;

namespace NeighborGoods.Api.Features.Listing.Services;

public sealed class ListingFavoriteService(
    NeighborGoodsDbContext dbContext,
    IBlobStorage blobStorage)
{
    public async Task<(FavoriteToggleDto? Data, string? ErrorCode, string? ErrorMessage)> FavoriteAsync(
        string userId,
        Guid listingId,
        CancellationToken cancellationToken = default)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Id == listingId)
            .Select(x => new { x.Id, x.SellerId, x.Status, x.Category })
            .FirstOrDefaultAsync(cancellationToken);
        if (listing is null)
        {
            return (null, "LISTING_NOT_FOUND", "找不到商品");
        }

        if (!IsVisibleStatus(listing.Status))
        {
            return (null, "LISTING_NOT_AVAILABLE", "此商品目前無法收藏");
        }

        if (string.Equals(listing.SellerId, userId, StringComparison.Ordinal))
        {
            return (null, "LISTING_FAVORITE_OWN_LISTING_NOT_ALLOWED", "不可收藏自己的商品");
        }

        var existing = await dbContext.ListingFavorites
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ListingId == listingId, cancellationToken);

        if (existing is null)
        {
            dbContext.ListingFavorites.Add(new ListingFavorite
            {
                Id = Guid.NewGuid(),
                ListingId = listingId,
                UserId = userId,
                CategorySnapshot = listing.Category,
                CreatedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);

            existing = await dbContext.ListingFavorites
                .AsNoTracking()
                .FirstAsync(x => x.UserId == userId && x.ListingId == listingId, cancellationToken);
        }

        var favoriteCount = await dbContext.ListingFavorites
            .CountAsync(x => x.ListingId == listingId, cancellationToken);

        return (new FavoriteToggleDto(listingId, true, favoriteCount, existing.CreatedAt), null, null);
    }

    public async Task<(FavoriteToggleDto? Data, string? ErrorCode, string? ErrorMessage)> UnfavoriteAsync(
        string userId,
        Guid listingId,
        CancellationToken cancellationToken = default)
    {
        var listingExists = await dbContext.Listings
            .AsNoTracking()
            .AnyAsync(x => x.Id == listingId, cancellationToken);
        if (!listingExists)
        {
            return (null, "LISTING_NOT_FOUND", "找不到商品");
        }

        var favorite = await dbContext.ListingFavorites
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ListingId == listingId, cancellationToken);

        if (favorite is not null)
        {
            dbContext.ListingFavorites.Remove(favorite);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var favoriteCount = await dbContext.ListingFavorites
            .CountAsync(x => x.ListingId == listingId, cancellationToken);

        return (new FavoriteToggleDto(listingId, false, favoriteCount, null), null, null);
    }

    public async Task<(FavoriteStatusDto? Data, string? ErrorCode, string? ErrorMessage)> GetFavoriteStatusAsync(
        string? userId,
        Guid listingId,
        CancellationToken cancellationToken = default)
    {
        var listingExists = await dbContext.Listings
            .AsNoTracking()
            .AnyAsync(x => x.Id == listingId, cancellationToken);
        if (!listingExists)
        {
            return (null, "LISTING_NOT_FOUND", "找不到商品");
        }

        var favoriteCount = await dbContext.ListingFavorites
            .CountAsync(x => x.ListingId == listingId, cancellationToken);
        var isFavorited = false;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            isFavorited = await dbContext.ListingFavorites
                .AnyAsync(x => x.ListingId == listingId && x.UserId == userId, cancellationToken);
        }

        return (new FavoriteStatusDto(listingId, favoriteCount, isFavorited), null, null);
    }

    public async Task<PagedResult<MyFavoriteListItemDto>> GetMyFavoritesAsync(
        string userId,
        int page,
        int pageSize,
        int? categoryCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (normalizedPage - 1) * normalizedPageSize;

        var query = dbContext.ListingFavorites
            .AsNoTracking()
            .Where(x => x.UserId == userId);
        if (categoryCode is { } cat)
        {
            query = query.Where(x => x.CategorySnapshot == cat);
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(normalizedPageSize)
            .Join(
                dbContext.Listings.AsNoTracking(),
                favorite => favorite.ListingId,
                listing => listing.Id,
                (favorite, listing) => new
                {
                    favorite.ListingId,
                    favorite.CreatedAt,
                    listing.Title,
                    listing.Category,
                    listing.Price,
                    listing.IsFree
                })
            .ToListAsync(cancellationToken);

        var categoryNameMap = await dbContext.ListingCategories
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        var listingIds = rows.Select(x => x.ListingId).Distinct().ToList();
        var imageMap = await GetCoverImageMapAsync(listingIds, cancellationToken);

        var items = rows
            .Select(x => new MyFavoriteListItemDto(
                x.ListingId,
                x.Title,
                x.Category,
                categoryNameMap.GetValueOrDefault(x.Category, "其他"),
                Convert.ToInt32(x.Price),
                x.IsFree,
                imageMap.GetValueOrDefault(x.ListingId),
                x.CreatedAt))
            .ToList();

        return new PagedResult<MyFavoriteListItemDto>(normalizedPage, normalizedPageSize, total, items);
    }

    public async Task<InterestProfileDto> GetInterestProfileAsync(
        string userId,
        int days,
        int topN,
        CancellationToken cancellationToken = default)
    {
        var normalizedDays = Math.Clamp(days, 1, 365);
        var normalizedTopN = Math.Clamp(topN, 1, 20);
        var from = DateTime.UtcNow.AddDays(-normalizedDays);

        var grouped = await dbContext.ListingFavorites
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.CreatedAt >= from)
            .GroupBy(x => x.CategorySnapshot)
            .Select(g => new { CategoryCode = g.Key, FavoriteCount = g.Count() })
            .OrderByDescending(x => x.FavoriteCount)
            .ThenBy(x => x.CategoryCode)
            .Take(normalizedTopN)
            .ToListAsync(cancellationToken);

        var categoryNameMap = await dbContext.ListingCategories
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        var categories = grouped
            .Select(x => new InterestCategoryDto(
                x.CategoryCode,
                categoryNameMap.GetValueOrDefault(x.CategoryCode, "其他"),
                x.FavoriteCount,
                x.FavoriteCount))
            .ToList();

        return new InterestProfileDto(userId, normalizedDays, categories, DateTime.UtcNow);
    }

    public async Task<(PushTargetsResultDto? Data, string? ErrorCode, string? ErrorMessage)> GetPushTargetsAsync(
        int categoryCode,
        Guid? listingId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var categoryName = await dbContext.ListingCategories
            .AsNoTracking()
            .Where(x => x.Id == categoryCode)
            .Select(x => x.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);
        if (categoryName is null)
        {
            return (null, "CATEGORY_NOT_FOUND", "找不到商品分類");
        }

        string? sellerId = null;
        if (listingId.HasValue)
        {
            sellerId = await dbContext.Listings
                .AsNoTracking()
                .Where(x => x.Id == listingId.Value)
                .Select(x => x.SellerId)
                .FirstOrDefaultAsync(cancellationToken);
            if (sellerId is null)
            {
                return (null, "LISTING_NOT_FOUND", "找不到商品");
            }
        }

        var normalizedLimit = Math.Clamp(limit, 1, 500);

        var candidates = await dbContext.ListingFavorites
            .AsNoTracking()
            .Where(x => x.CategorySnapshot == categoryCode)
            .GroupBy(x => x.UserId)
            .Select(g => new { UserId = g.Key, Score = g.Count() })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.UserId)
            .Take(normalizedLimit + 50)
            .ToListAsync(cancellationToken);

        var candidateIds = candidates.Select(x => x.UserId).ToList();
        var users = await dbContext.AspNetUsers
            .AsNoTracking()
            .Where(x => candidateIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.EmailConfirmed,
                x.EmailNotificationEnabled,
                x.LineMessagingApiUserId
            })
            .ToListAsync(cancellationToken);

        var userMap = users.ToDictionary(x => x.Id, x => x);
        var targets = new List<PushTargetDto>(normalizedLimit);
        foreach (var candidate in candidates)
        {
            if (targets.Count >= normalizedLimit)
            {
                break;
            }

            if (sellerId is not null && string.Equals(candidate.UserId, sellerId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!userMap.TryGetValue(candidate.UserId, out var user))
            {
                continue;
            }

            var canLine = !string.IsNullOrWhiteSpace(user.LineMessagingApiUserId);
            var canEmail = user.EmailNotificationEnabled && user.EmailConfirmed && !string.IsNullOrWhiteSpace(user.Email);
            if (!canLine && !canEmail)
            {
                continue;
            }

            targets.Add(new PushTargetDto(
                user.Id,
                user.LineMessagingApiUserId,
                canEmail ? user.Email : null,
                candidate.Score));
        }

        return (new PushTargetsResultDto(listingId, categoryCode, categoryName, targets), null, null);
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

    private static bool IsVisibleStatus(int statusCode) =>
        statusCode == (int)ListingStatus.Active || statusCode == (int)ListingStatus.Reserved;
}
