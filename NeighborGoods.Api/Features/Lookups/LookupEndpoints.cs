using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Shared.ApiContracts;
using NeighborGoods.Api.Shared.Persistence;

namespace NeighborGoods.Api.Features.Lookups;

public static class LookupEndpoints
{
    public static IEndpointRouteBuilder MapLookupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/lookups/conditions", async (
            HttpContext httpContext,
            NeighborGoodsDbContext dbContext,
            CancellationToken ct = default) =>
        {
            var items = await dbContext.ListingConditions.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Select(c => new
                {
                    id = c.Id,
                    codeKey = c.CodeKey,
                    displayName = c.DisplayName,
                    sortOrder = c.SortOrder
                })
                .ToListAsync(ct);

            return Results.Ok(ApiResponseFactory.Success(items, httpContext));
        })
        .WithName("GetListingConditionsLookupV1")
        .WithSummary("取得商品品況選單");

        app.MapGet("/api/v1/lookups/residences", async (
            HttpContext httpContext,
            NeighborGoodsDbContext dbContext,
            CancellationToken ct = default) =>
        {
            var items = await dbContext.ListingResidences.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Select(c => new
                {
                    id = c.Id,
                    codeKey = c.CodeKey,
                    displayName = c.DisplayName,
                    sortOrder = c.SortOrder
                })
                .ToListAsync(ct);

            return Results.Ok(ApiResponseFactory.Success(items, httpContext));
        })
        .WithName("GetListingResidencesLookupV1")
        .WithSummary("取得商品社宅選單");

        app.MapGet("/api/v1/lookups/pickup-locations", async (
            HttpContext httpContext,
            NeighborGoodsDbContext dbContext,
            CancellationToken ct = default) =>
        {
            var items = await dbContext.ListingPickupLocations.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Select(c => new
                {
                    id = c.Id,
                    codeKey = c.CodeKey,
                    displayName = c.DisplayName,
                    sortOrder = c.SortOrder
                })
                .ToListAsync(ct);

            return Results.Ok(ApiResponseFactory.Success(items, httpContext));
        })
        .WithName("GetListingPickupLocationsLookupV1")
        .WithSummary("取得商品面交地點選單");

        app.MapGet("/api/v1/lookups/categories", async (
            HttpContext httpContext,
            NeighborGoodsDbContext dbContext,
            CancellationToken ct = default) =>
        {
            var items = await dbContext.ListingCategories.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Select(c => new
                {
                    id = c.Id,
                    codeKey = c.CodeKey,
                    displayName = c.DisplayName,
                    sortOrder = c.SortOrder
                })
                .ToListAsync(ct);

            return Results.Ok(ApiResponseFactory.Success(items, httpContext));
        })
        .WithName("GetListingCategoriesLookupV1")
        .WithSummary("取得商品分類選單");

        return app;
    }
}
