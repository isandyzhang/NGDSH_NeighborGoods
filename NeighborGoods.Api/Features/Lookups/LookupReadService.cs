using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NeighborGoods.Data;

namespace NeighborGoods.Api.Features.Lookups;

public sealed record CachedLookupDto(int Id, string CodeKey, string DisplayName, int SortOrder);

public sealed class LookupReadService(IMemoryCache memoryCache, NeighborGoodsDbContext dbContext)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public Task<IReadOnlyList<CachedLookupDto>> GetConditionsAsync(CancellationToken cancellationToken = default) =>
        GetOrCreateAsync(
            "lookups:v1:conditions",
            dbContext.ListingConditions.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Select(c => new CachedLookupDto(c.Id, c.CodeKey, c.DisplayName, c.SortOrder)),
            cancellationToken);

    public Task<IReadOnlyList<CachedLookupDto>> GetResidencesAsync(CancellationToken cancellationToken = default) =>
        GetOrCreateAsync(
            "lookups:v1:residences",
            dbContext.ListingResidences.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Select(c => new CachedLookupDto(c.Id, c.CodeKey, c.DisplayName, c.SortOrder)),
            cancellationToken);

    public Task<IReadOnlyList<CachedLookupDto>> GetPickupLocationsAsync(CancellationToken cancellationToken = default) =>
        GetOrCreateAsync(
            "lookups:v1:pickup-locations",
            dbContext.ListingPickupLocations.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Select(c => new CachedLookupDto(c.Id, c.CodeKey, c.DisplayName, c.SortOrder)),
            cancellationToken);

    public Task<IReadOnlyList<CachedLookupDto>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        GetOrCreateAsync(
            "lookups:v1:categories",
            dbContext.ListingCategories.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Select(c => new CachedLookupDto(c.Id, c.CodeKey, c.DisplayName, c.SortOrder)),
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
