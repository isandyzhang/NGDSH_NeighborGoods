using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Admin.Contracts.Requests;
using NeighborGoods.Api.Features.Admin.Contracts.Responses;
using NeighborGoods.Api.Features.Lookups;
using NeighborGoods.Data;
using NeighborGoods.Data.Listings;

namespace NeighborGoods.Api.Features.Admin.Services;

public sealed class AdminHousingService(NeighborGoodsDbContext dbContext, LookupCache lookupCache)
{
    public const int UnknownResidenceId = 0;
    public const string MessagePickupCodeKey = "Message";

    public async Task<IReadOnlyList<AdminResidenceResponse>> ListResidencesAsync(CancellationToken cancellationToken)
    {
        var listingCounts = await dbContext.Listings
            .AsNoTracking()
            .GroupBy(x => x.Residence)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);

        var pickupCounts = await dbContext.ListingPickupLocations
            .AsNoTracking()
            .Where(x => x.ResidenceId != null)
            .GroupBy(x => x.ResidenceId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);

        var rows = await dbContext.ListingResidences
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => ToResidenceResponse(
                x,
                listingCounts.GetValueOrDefault(x.Id),
                pickupCounts.GetValueOrDefault(x.Id)))
            .ToList();
    }

    public async Task<AdminResidenceResponse> CreateResidenceAsync(
        UpsertAdminResidenceRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeResidence(request);
        var entity = new ListingResidence
        {
            Id = await NextIdAsync<ListingResidence>(cancellationToken),
            DisplayName = normalized.DisplayName,
            City = normalized.City,
            District = normalized.District,
            Notes = normalized.Notes,
            SortOrder = normalized.SortOrder,
            IsActive = normalized.IsActive,
        };
        entity.CodeKey = $"residence-{entity.Id}";

        await dbContext.ListingResidences.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        lookupCache.InvalidateAll();
        return ToResidenceResponse(entity, 0, 0);
    }

    public async Task<AdminResidenceResponse> UpdateResidenceAsync(
        int id,
        UpsertAdminResidenceRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.ListingResidences.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFound("RESIDENCE_NOT_FOUND", "找不到社宅");

        var normalized = NormalizeResidence(request);
        entity.DisplayName = normalized.DisplayName;
        entity.City = normalized.City;
        entity.District = normalized.District;
        entity.Notes = normalized.Notes;
        entity.SortOrder = normalized.SortOrder;
        entity.IsActive = normalized.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        lookupCache.InvalidateAll();
        return await GetResidenceResponseAsync(entity, cancellationToken);
    }

    public async Task<AdminResidenceResponse> SetResidenceEnabledAsync(
        int id,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.ListingResidences.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFound("RESIDENCE_NOT_FOUND", "找不到社宅");

        entity.IsActive = isEnabled;
        await dbContext.SaveChangesAsync(cancellationToken);
        lookupCache.InvalidateAll();
        return await GetResidenceResponseAsync(entity, cancellationToken);
    }

    public async Task DeleteResidenceAsync(int id, CancellationToken cancellationToken)
    {
        if (id == UnknownResidenceId)
        {
            throw Conflict("LOOKUP_PROTECTED", "系統預設社宅不可刪除");
        }

        var entity = await dbContext.ListingResidences.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFound("RESIDENCE_NOT_FOUND", "找不到社宅");

        var listingCount = await dbContext.Listings.CountAsync(x => x.Residence == id, cancellationToken);
        if (listingCount > 0)
        {
            throw Conflict("LOOKUP_IN_USE", "仍有商品使用此社宅，請改為停用");
        }

        var pickupCount = await dbContext.ListingPickupLocations.CountAsync(x => x.ResidenceId == id, cancellationToken);
        if (pickupCount > 0)
        {
            throw Conflict("LOOKUP_IN_USE", "請先刪除該社宅的面交地點");
        }

        dbContext.ListingResidences.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        lookupCache.InvalidateAll();
    }

    public async Task<IReadOnlyList<AdminPickupLocationResponse>> ListPickupLocationsAsync(
        int? residenceId,
        bool sharedOnly,
        CancellationToken cancellationToken)
    {
        var listingCounts = await dbContext.Listings
            .AsNoTracking()
            .GroupBy(x => x.PickupLocation)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);

        var query = dbContext.ListingPickupLocations.AsNoTracking().AsQueryable();
        if (sharedOnly)
        {
            query = query.Where(x => x.ResidenceId == null);
        }
        else if (residenceId.HasValue)
        {
            query = query.Where(x => x.ResidenceId == residenceId.Value);
        }

        var rows = await query
            .Include(x => x.Residence)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => ToPickupResponse(x, x.Residence?.DisplayName, listingCounts.GetValueOrDefault(x.Id)))
            .ToList();
    }

    public async Task<AdminPickupLocationResponse> CreatePickupLocationAsync(
        UpsertAdminPickupLocationRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = await NormalizePickupAsync(request, existing: null, cancellationToken);
        var entity = new ListingPickupLocation
        {
            Id = await NextIdAsync<ListingPickupLocation>(cancellationToken),
            DisplayName = normalized.DisplayName,
            ResidenceId = normalized.ResidenceId,
            SortOrder = normalized.SortOrder,
            IsActive = normalized.IsActive,
        };
        entity.CodeKey = $"pickup-{entity.Id}";

        await dbContext.ListingPickupLocations.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        lookupCache.InvalidateAll();
        return await GetPickupResponseAsync(entity, cancellationToken);
    }

    public async Task<AdminPickupLocationResponse> UpdatePickupLocationAsync(
        int id,
        UpsertAdminPickupLocationRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.ListingPickupLocations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFound("PICKUP_LOCATION_NOT_FOUND", "找不到面交地點");

        var normalized = await NormalizePickupAsync(request, entity, cancellationToken);
        entity.DisplayName = normalized.DisplayName;
        entity.ResidenceId = normalized.ResidenceId;
        entity.SortOrder = normalized.SortOrder;
        entity.IsActive = normalized.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        lookupCache.InvalidateAll();
        return await GetPickupResponseAsync(entity, cancellationToken);
    }

    public async Task<AdminPickupLocationResponse> SetPickupLocationEnabledAsync(
        int id,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.ListingPickupLocations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFound("PICKUP_LOCATION_NOT_FOUND", "找不到面交地點");

        entity.IsActive = isEnabled;
        await dbContext.SaveChangesAsync(cancellationToken);
        lookupCache.InvalidateAll();
        return await GetPickupResponseAsync(entity, cancellationToken);
    }

    public async Task DeletePickupLocationAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.ListingPickupLocations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFound("PICKUP_LOCATION_NOT_FOUND", "找不到面交地點");

        if (IsProtectedPickup(entity))
        {
            throw Conflict("LOOKUP_PROTECTED", "系統預設面交地點不可刪除");
        }

        var listingCount = await dbContext.Listings.CountAsync(x => x.PickupLocation == id, cancellationToken);
        if (listingCount > 0)
        {
            throw Conflict("LOOKUP_IN_USE", "仍有商品使用此面交地點，請改為停用");
        }

        dbContext.ListingPickupLocations.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        lookupCache.InvalidateAll();
    }

    public async Task<IReadOnlyList<AdminCategoryResponse>> ListCategoriesAsync(CancellationToken cancellationToken)
    {
        var listingCounts = await dbContext.Listings
            .AsNoTracking()
            .GroupBy(x => x.Category)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);

        var rows = await dbContext.ListingCategories
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(x => ToCategoryResponse(x, listingCounts.GetValueOrDefault(x.Id))).ToList();
    }

    public async Task<AdminCategoryResponse> CreateCategoryAsync(
        UpsertAdminCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeCategory(request);
        var entity = new ListingCategory
        {
            Id = await NextIdAsync<ListingCategory>(cancellationToken),
            DisplayName = normalized.DisplayName,
            SortOrder = normalized.SortOrder,
            IsActive = normalized.IsActive,
        };
        entity.CodeKey = $"category-{entity.Id}";

        await dbContext.ListingCategories.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        lookupCache.InvalidateAll();
        return ToCategoryResponse(entity, 0);
    }

    public async Task<AdminCategoryResponse> UpdateCategoryAsync(
        int id,
        UpsertAdminCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.ListingCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFound("CATEGORY_NOT_FOUND", "找不到商品分類");

        var normalized = NormalizeCategory(request);
        entity.DisplayName = normalized.DisplayName;
        entity.SortOrder = normalized.SortOrder;
        entity.IsActive = normalized.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        lookupCache.InvalidateAll();
        return ToCategoryResponse(
            entity,
            await dbContext.Listings.CountAsync(x => x.Category == id, cancellationToken));
    }

    public async Task<AdminCategoryResponse> SetCategoryEnabledAsync(
        int id,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.ListingCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFound("CATEGORY_NOT_FOUND", "找不到商品分類");

        entity.IsActive = isEnabled;
        await dbContext.SaveChangesAsync(cancellationToken);
        lookupCache.InvalidateAll();
        return ToCategoryResponse(
            entity,
            await dbContext.Listings.CountAsync(x => x.Category == id, cancellationToken));
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.ListingCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFound("CATEGORY_NOT_FOUND", "找不到商品分類");

        var listingCount = await dbContext.Listings.CountAsync(x => x.Category == id, cancellationToken);
        if (listingCount > 0)
        {
            throw Conflict("LOOKUP_IN_USE", "仍有商品使用此分類，請改為停用");
        }

        dbContext.ListingCategories.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        lookupCache.InvalidateAll();
    }

    private async Task<int> NextIdAsync<TEntity>(CancellationToken cancellationToken)
        where TEntity : class, IListingLookup
    {
        var max = await dbContext.Set<TEntity>().Select(x => (int?)x.Id).MaxAsync(cancellationToken);
        return (max ?? -1) + 1;
    }

    private static UpsertAdminResidenceRequest NormalizeResidence(UpsertAdminResidenceRequest request)
    {
        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length is < 1 or > 128)
        {
            throw Validation("社宅名稱為必填，最多 128 字");
        }

        var city = NormalizeOptional(request.City, 32);
        if (!TaiwanCities.IsValid(city))
        {
            throw Validation("縣市不在允許清單中");
        }

        return request with
        {
            DisplayName = displayName,
            City = city,
            District = NormalizeOptional(request.District, 32),
            Notes = NormalizeOptional(request.Notes, 500),
        };
    }

    private async Task<UpsertAdminPickupLocationRequest> NormalizePickupAsync(
        UpsertAdminPickupLocationRequest request,
        ListingPickupLocation? existing,
        CancellationToken cancellationToken)
    {
        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length is < 1 or > 128)
        {
            throw Validation("面交地點名稱為必填，最多 128 字");
        }

        if (existing is not null && IsProtectedPickup(existing) && request.ResidenceId is not null)
        {
            throw Validation("系統預設面交地點不可綁定特定社宅");
        }

        if (request.ResidenceId is { } residenceId)
        {
            var residenceExists = await dbContext.ListingResidences.AnyAsync(x => x.Id == residenceId, cancellationToken);
            if (!residenceExists)
            {
                throw Validation("找不到所屬社宅");
            }
        }

        return request with { DisplayName = displayName };
    }

    private static UpsertAdminCategoryRequest NormalizeCategory(UpsertAdminCategoryRequest request)
    {
        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length is < 1 or > 128)
        {
            throw Validation("分類名稱為必填，最多 128 字");
        }

        return request with { DisplayName = displayName };
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw Validation($"欄位最多 {maxLength} 字");
        }

        return trimmed;
    }

    private static bool IsProtectedPickup(ListingPickupLocation entity) =>
        string.Equals(entity.CodeKey, MessagePickupCodeKey, StringComparison.Ordinal);

    private async Task<AdminResidenceResponse> GetResidenceResponseAsync(
        ListingResidence entity,
        CancellationToken cancellationToken)
    {
        var listingCount = await dbContext.Listings.CountAsync(x => x.Residence == entity.Id, cancellationToken);
        var pickupCount = await dbContext.ListingPickupLocations.CountAsync(x => x.ResidenceId == entity.Id, cancellationToken);
        return ToResidenceResponse(entity, listingCount, pickupCount);
    }

    private async Task<AdminPickupLocationResponse> GetPickupResponseAsync(
        ListingPickupLocation entity,
        CancellationToken cancellationToken)
    {
        string? residenceName = null;
        if (entity.ResidenceId is { } residenceId)
        {
            residenceName = await dbContext.ListingResidences
                .AsNoTracking()
                .Where(x => x.Id == residenceId)
                .Select(x => x.DisplayName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var listingCount = await dbContext.Listings.CountAsync(x => x.PickupLocation == entity.Id, cancellationToken);
        return ToPickupResponse(entity, residenceName, listingCount);
    }

    private static AdminResidenceResponse ToResidenceResponse(
        ListingResidence entity,
        int listingCount,
        int pickupCount) =>
        new(
            entity.Id,
            entity.CodeKey,
            entity.DisplayName,
            entity.City,
            entity.District,
            entity.Notes,
            entity.SortOrder,
            entity.IsActive,
            listingCount,
            pickupCount);

    private static AdminPickupLocationResponse ToPickupResponse(
        ListingPickupLocation entity,
        string? residenceName,
        int listingCount) =>
        new(
            entity.Id,
            entity.CodeKey,
            entity.DisplayName,
            entity.ResidenceId,
            residenceName,
            entity.SortOrder,
            entity.IsActive,
            listingCount,
            IsProtectedPickup(entity));

    private static AdminCategoryResponse ToCategoryResponse(ListingCategory entity, int listingCount) =>
        new(entity.Id, entity.CodeKey, entity.DisplayName, entity.SortOrder, entity.IsActive, listingCount);

    private static AdminHousingException Validation(string message) =>
        new("VALIDATION_ERROR", message, StatusCodes.Status400BadRequest);

    private static AdminHousingException NotFound(string code, string message) =>
        new(code, message, StatusCodes.Status404NotFound);

    private static AdminHousingException Conflict(string code, string message) =>
        new(code, message, StatusCodes.Status409Conflict);
}
