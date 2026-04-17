using NeighborGoods.Api.Features.Listing.Contracts;
using NeighborGoods.Api.Features.Listing.Services;
using NeighborGoods.Api.Shared.ApiContracts;

namespace NeighborGoods.Api.Features.Listing;

public static class ListingEndpoints
{
    public static IEndpointRouteBuilder MapListingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/listings/{id:guid}", async (
            HttpContext httpContext,
            ListingQueryService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            var result = await service.GetByIdAsync(id, ct);
            if (result is null)
            {
                return Results.NotFound(ApiResponseFactory.Error("LISTING_NOT_FOUND", "找不到商品", httpContext));
            }

            return Results.Ok(ApiResponseFactory.Success(result, httpContext));
        })
        .WithName("GetListingByIdV1");

        app.MapGet("/api/v1/listings", async (
            HttpContext httpContext,
            ListingQueryService service,
            string? q,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var request = new ListingQueryRequest
            {
                Query = q,
                Page = page,
                PageSize = pageSize
            };

            var result = await service.QueryAsync(request, ct);
            var payload = new
            {
                items = result.Items,
                pagination = new
                {
                    page = result.Page,
                    pageSize = result.PageSize,
                    totalCount = result.Total,
                    totalPages = (int)Math.Ceiling(result.Total / (double)result.PageSize)
                }
            };
            return Results.Ok(ApiResponseFactory.Success(payload, httpContext));
        })
        .WithName("GetListingsV1");

        app.MapPost("/api/v1/listings", async (
            HttpContext httpContext,
            ListingCommandService service,
            CreateListingRequest request,
            CancellationToken ct = default) =>
        {
            try
            {
                var id = await service.CreateAsync(request, ct);
                var payload = new { id };
                return Results.Created($"/api/v1/listings/{id}", ApiResponseFactory.Success(payload, httpContext));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ApiResponseFactory.Error("VALIDATION_ERROR", ex.Message, httpContext));
            }
            catch (ListingAccessException ex)
            {
                return Results.Json(
                    ApiResponseFactory.Error(ex.Code, ex.Message, httpContext),
                    statusCode: ex.StatusCode);
            }
        })
        .WithName("CreateListingV1")
        .RequireAuthorization();

        app.MapPut("/api/v1/listings/{id:guid}", async (
            HttpContext httpContext,
            ListingCommandService service,
            Guid id,
            UpdateListingRequest request,
            CancellationToken ct = default) =>
        {
            try
            {
                var updated = await service.UpdateAsync(id, request, ct);
                if (!updated)
                {
                    return Results.NotFound(ApiResponseFactory.Error("LISTING_NOT_FOUND", "找不到商品", httpContext));
                }

                var payload = new { id };
                return Results.Ok(ApiResponseFactory.Success(payload, httpContext));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ApiResponseFactory.Error("VALIDATION_ERROR", ex.Message, httpContext));
            }
        })
        .WithName("UpdateListingV1")
        .RequireAuthorization();

        app.MapDelete("/api/v1/listings/{id:guid}", async (
            HttpContext httpContext,
            ListingCommandService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            var deleted = await service.DeleteAsync(id, ct);
            if (!deleted)
            {
                return Results.NotFound(ApiResponseFactory.Error("LISTING_NOT_FOUND", "找不到商品", httpContext));
            }

            return Results.Ok(ApiResponseFactory.Success(new { id, deleted = true }, httpContext));
        })
        .WithName("DeleteListingV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/listings/{id:guid}/reserve", async (
            HttpContext httpContext,
            ListingStatusService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            var result = await service.ReserveAsync(id, ct);
            return ToStatusActionResult(result, id, httpContext);
        })
        .WithName("ReserveListingV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/listings/{id:guid}/activate", async (
            HttpContext httpContext,
            ListingStatusService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            var result = await service.ActivateAsync(id, ct);
            return ToStatusActionResult(result, id, httpContext);
        })
        .WithName("ActivateListingV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/listings/{id:guid}/sold", async (
            HttpContext httpContext,
            ListingStatusService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            var result = await service.MarkSoldAsync(id, ct);
            return ToStatusActionResult(result, id, httpContext);
        })
        .WithName("MarkListingSoldV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/listings/{id:guid}/archive", async (
            HttpContext httpContext,
            ListingStatusService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            var result = await service.ArchiveAsync(id, ct);
            return ToStatusActionResult(result, id, httpContext);
        })
        .WithName("ArchiveListingV1")
        .RequireAuthorization();

        return app;
    }

    private static IResult ToStatusActionResult(ListingStatusChangeResult result, Guid id, HttpContext httpContext)
    {
        return result switch
        {
            ListingStatusChangeResult.Success =>
                Results.Ok(ApiResponseFactory.Success(new { id }, httpContext)),
            ListingStatusChangeResult.NotFound =>
                Results.NotFound(ApiResponseFactory.Error("LISTING_NOT_FOUND", "找不到商品", httpContext)),
            ListingStatusChangeResult.InvalidCurrentStatus =>
                Results.BadRequest(ApiResponseFactory.Error("VALIDATION_ERROR", "商品狀態資料無效", httpContext)),
            ListingStatusChangeResult.InvalidTransition =>
                Results.Conflict(ApiResponseFactory.Error("LISTING_INVALID_STATUS_TRANSITION", "目前狀態不可執行此操作", httpContext)),
            _ =>
                Results.BadRequest(ApiResponseFactory.Error("VALIDATION_ERROR", "狀態操作失敗", httpContext))
        };
    }
}
