using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NeighborGoods.Data;
using NeighborGoods.Data.Listings;

namespace NeighborGoods.Api.Features.Lookups;

public sealed record CachedLookupDto(int Id, string CodeKey, string DisplayName, int SortOrder, int? ResidenceId);

public sealed class LookupReadService(IMemoryCache memoryCache, NeighborGoodsDbContext dbContext)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public Task<IReadOnlyList<CachedLookupDto>> GetConditionsAsync(CancellationToken cancellationToken = default) =>
        GetOrCreateAsync(
            LookupCacheKeys.Conditions,
            dbContext.ListingConditions.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Select(c => new CachedLookupDto(c.Id, c.CodeKey, c.DisplayName, c.SortOrder, null)),
            cancellationToken);

    public Task<IReadOnlyList<CachedLookupDto>> GetResidencesAsync(CancellationToken cancellationToken = default) =>
        GetOrCreateAsync(
            LookupCacheKeys.Residences,
            dbContext.ListingResidences.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Select(c => new CachedLookupDto(c.Id, c.CodeKey, c.DisplayName, c.SortOrder, null)),
            cancellationToken);

    public async Task<IReadOnlyList<CachedLookupDto>> GetPickupLocationsAsync(
        int? residenceId = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await GetOrCreateAsync(
            LookupCacheKeys.PickupLocations,
            dbContext.ListingPickupLocations.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Id)
                .Select(c => new CachedLookupDto(c.Id, c.CodeKey, c.DisplayName, c.SortOrder, c.ResidenceId)),
            cancellationToken);

        if (residenceId is null)
        {
            return rows;
        }

        return rows
            .Where(x => x.ResidenceId is null || x.ResidenceId == residenceId)
            .ToList();
    }

    public Task<IReadOnlyList<CachedLookupDto>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        GetOrCreateAsync(
            LookupCacheKeys.Categories,
            dbContext.ListingCategories.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Select(c => new CachedLookupDto(c.Id, c.CodeKey, c.DisplayName, c.SortOrder, null)),
            cancellationToken);

    private async Task<IReadOnlyList<CachedLookupDto>> GetOrCreateAsync(
        string key,
        IQueryable<CachedLookupDto> query,
        CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue<IReadOnlyList<CachedLookupDto>>(key, out var cached) && cached is not null)
        {
            return cached;
        }

        var list = await query.ToListAsync(cancellationToken);
        memoryCache.Set(key, list, CacheDuration);
        return list;
    }
}
