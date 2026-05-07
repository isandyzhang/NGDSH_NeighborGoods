using NeighborGoods.Api.Shared.ApiContracts;

namespace NeighborGoods.Api.Features.Lookups;

public static class LookupEndpoints
{
    public static IEndpointRouteBuilder MapLookupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/lookups/conditions", async (
            HttpContext httpContext,
            LookupReadService lookupReadService,
            CancellationToken ct = default) =>
        {
            var rows = await lookupReadService.GetConditionsAsync(ct);
            var items = ToJsonRows(rows);
            return Results.Ok(ApiResponseFactory.Success(items, httpContext));
        })
        .WithName("GetListingConditionsLookupV1")
        .WithSummary("取得商品品況選單");

        app.MapGet("/api/v1/lookups/residences", async (
            HttpContext httpContext,
            LookupReadService lookupReadService,
            CancellationToken ct = default) =>
        {
            var rows = await lookupReadService.GetResidencesAsync(ct);
            var items = ToJsonRows(rows);
            return Results.Ok(ApiResponseFactory.Success(items, httpContext));
        })
        .WithName("GetListingResidencesLookupV1")
        .WithSummary("取得商品社宅選單");

        app.MapGet("/api/v1/lookups/pickup-locations", async (
            HttpContext httpContext,
            LookupReadService lookupReadService,
            CancellationToken ct = default) =>
        {
            var rows = await lookupReadService.GetPickupLocationsAsync(ct);
            var items = ToJsonRows(rows);
            return Results.Ok(ApiResponseFactory.Success(items, httpContext));
        })
        .WithName("GetListingPickupLocationsLookupV1")
        .WithSummary("取得商品面交地點選單");

        app.MapGet("/api/v1/lookups/categories", async (
            HttpContext httpContext,
            LookupReadService lookupReadService,
            CancellationToken ct = default) =>
        {
            var rows = await lookupReadService.GetCategoriesAsync(ct);
            var items = ToJsonRows(rows);
            return Results.Ok(ApiResponseFactory.Success(items, httpContext));
        })
        .WithName("GetListingCategoriesLookupV1")
        .WithSummary("取得商品分類選單");

        return app;
    }

    private static List<object> ToJsonRows(IReadOnlyList<CachedLookupDto> rows)
    {
        var list = new List<object>(rows.Count);
        foreach (var r in rows)
        {
            list.Add(new { id = r.Id, codeKey = r.CodeKey, displayName = r.DisplayName, sortOrder = r.SortOrder });
        }

        return list;
    }
}
