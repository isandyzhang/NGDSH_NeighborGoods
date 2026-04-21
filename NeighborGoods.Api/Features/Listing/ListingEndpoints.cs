using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Listing.Contracts;
using NeighborGoods.Api.Features.Listing.Services;
using NeighborGoods.Api.Shared.ApiContracts;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Security;

namespace NeighborGoods.Api.Features.Listing;

public static class ListingEndpoints
{
    private static readonly JsonSerializerOptions CreateListingPayloadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
            bool? isFree = null,
            bool? isCharity = null,
            bool? isTradeable = null,
            int? categoryCode = null,
            int? conditionCode = null,
            int? residenceCode = null,
            int? minPrice = null,
            int? maxPrice = null,
            string? excludeUserId = null,
            CancellationToken ct = default) =>
        {
            var request = new ListingQueryRequest
            {
                Query = q,
                Page = page,
                PageSize = pageSize,
                IsFree = isFree,
                IsCharity = isCharity,
                IsTradeable = isTradeable,
                CategoryCode = categoryCode,
                ConditionCode = conditionCode,
                ResidenceCode = residenceCode,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                ExcludeUserId = excludeUserId
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
        .WithName("GetListingsV1")
        .WithSummary("商品列表（可累加篩選）")
        .WithDescription(
            "可選 query：isFree、isCharity、isTradeable、categoryCode、conditionCode、residenceCode、minPrice、maxPrice、excludeUserId。關鍵字 q 長度須 >= 2 才套用 LIKE。僅當 isFree/isCharity/isTradeable 為 true 時套用；多個同時帶入為 AND。列表排序：置頂中優先，再依建立時間。");

        app.MapGet("/api/v1/listings/mine", async (
            HttpContext httpContext,
            ListingQueryService service,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var request = new ListingQueryRequest { Page = page, PageSize = pageSize };
            var result = await service.QueryMineAsync(request, ct);
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
        .WithName("GetMyListingsV1")
        .WithSummary("我的商品（賣家本人所有狀態）")
        .RequireAuthorization();

        app.MapPost("/api/v1/listings/{id:guid}/favorite", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            ListingFavoriteService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await service.FavoriteAsync(userId, id, ct);
            if (data is null)
            {
                return ToFavoriteErrorResult(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("FavoriteListingV1")
        .RequireAuthorization();

        app.MapDelete("/api/v1/listings/{id:guid}/favorite", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            ListingFavoriteService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var (data, errorCode, errorMessage) = await service.UnfavoriteAsync(userId, id, ct);
            if (data is null)
            {
                return ToFavoriteErrorResult(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("UnfavoriteListingV1")
        .RequireAuthorization();

        app.MapGet("/api/v1/listings/{id:guid}/favorite-status", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            ListingFavoriteService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            var (data, errorCode, errorMessage) = await service.GetFavoriteStatusAsync(currentUser.UserId, id, ct);
            if (data is null)
            {
                return ToFavoriteErrorResult(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("GetListingFavoriteStatusV1");

        app.MapGet("/api/v1/listings/favorites", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            ListingFavoriteService service,
            int page = 1,
            int pageSize = 20,
            int? categoryCode = null,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var result = await service.GetMyFavoritesAsync(userId, page, pageSize, categoryCode, ct);
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
        .WithName("GetMyFavoriteListingsV1")
        .RequireAuthorization();

        app.MapGet("/api/v1/users/me/interest-profile", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            ListingFavoriteService service,
            int days = 90,
            int topN = 5,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var data = await service.GetInterestProfileAsync(userId, days, topN, ct);
            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("GetMyInterestProfileV1")
        .RequireAuthorization();

        app.MapGet("/api/v1/internal/push-targets", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            NeighborGoodsDbContext dbContext,
            ListingFavoriteService service,
            int categoryCode,
            Guid? listingId = null,
            int limit = 500,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var isAdmin = await dbContext.AspNetUsers
                .AsNoTracking()
                .AnyAsync(x => x.Id == userId && x.Role == 3, ct);
            if (!isAdmin)
            {
                return Results.Json(
                    ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var (data, errorCode, errorMessage) = await service.GetPushTargetsAsync(categoryCode, listingId, limit, ct);
            if (data is null)
            {
                return ToFavoriteErrorResult(httpContext, errorCode!, errorMessage!);
            }

            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        })
        .WithName("GetFavoritePushTargetsV1")
        .RequireAuthorization();

        app.MapPost("/api/v1/listings", async (
            HttpContext httpContext,
            ListingCommandService service,
            CancellationToken ct = default) =>
        {
            if (!httpContext.Request.HasFormContentType)
            {
                return Results.Json(
                    ApiResponseFactory.Error(
                        "UNSUPPORTED_MEDIA_TYPE",
                        "需使用 multipart/form-data：欄位 payload（JSON 字串）與 images（至少一張圖）。",
                        httpContext),
                    statusCode: StatusCodes.Status415UnsupportedMediaType);
            }

            try
            {
                var form = await httpContext.Request.ReadFormAsync(ct);
                var payloadRaw = form["payload"].ToString();
                if (string.IsNullOrWhiteSpace(payloadRaw))
                {
                    return Results.BadRequest(
                        ApiResponseFactory.Error("VALIDATION_ERROR", "缺少 payload（JSON）欄位。", httpContext));
                }

                CreateListingRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<CreateListingRequest>(payloadRaw, CreateListingPayloadJsonOptions);
                }
                catch (JsonException)
                {
                    return Results.BadRequest(
                        ApiResponseFactory.Error("VALIDATION_ERROR", "payload 不是有效的 JSON。", httpContext));
                }

                if (request is null)
                {
                    return Results.BadRequest(
                        ApiResponseFactory.Error("VALIDATION_ERROR", "payload 反序列化結果為空。", httpContext));
                }

                var files = form.Files.GetFiles("images");
                if (files.Count == 0)
                {
                    return Results.BadRequest(
                        ApiResponseFactory.Error(
                            "VALIDATION_ERROR",
                            "至少需要上傳一張商品照片（欄位名稱 images）。",
                            httpContext));
                }

                if (files.Count > ListingImageUploadRules.MaxImageCount)
                {
                    return Results.BadRequest(
                        ApiResponseFactory.Error(
                            "VALIDATION_ERROR",
                            $"最多只能上傳 {ListingImageUploadRules.MaxImageCount} 張照片。",
                            httpContext));
                }

                var id = await service.CreateWithImagesAsync(request, files, ct);
                var payload = new { id };
                return Results.Created($"/api/v1/listings/{id}", ApiResponseFactory.Success(payload, httpContext));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ApiResponseFactory.Error("VALIDATION_ERROR", ex.Message, httpContext));
            }
            catch (ListingAccessException ex)
            {
                return ToListingAccessResult(ex, httpContext);
            }
        })
        .WithName("CreateListingV1")
        .WithSummary("建立商品（multipart：payload JSON + images 至少一張；需已開啟 Email 通知）")
        .WithDescription(
            "Content-Type: multipart/form-data。欄位 **payload**：JSON 字串（與原 CreateListingRequest 相同：title、categoryCode、useTopPin 等）。欄位 **images**：重複同一欄位名稱可傳多檔，至少 1 張、最多 5 張；每檔須為允許的圖片 Content-Type 且不超過 5MB。")
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
            catch (ListingAccessException ex)
            {
                return ToListingAccessResult(ex, httpContext);
            }
        })
        .WithName("UpdateListingV1")
        .WithSummary("更新商品（需提供 pickupLocationCode）")
        .RequireAuthorization();

        app.MapDelete("/api/v1/listings/{id:guid}", async (
            HttpContext httpContext,
            ListingCommandService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            try
            {
                var deleted = await service.DeleteAsync(id, ct);
                if (!deleted)
                {
                    return Results.NotFound(ApiResponseFactory.Error("LISTING_NOT_FOUND", "找不到商品", httpContext));
                }

                return Results.Ok(ApiResponseFactory.Success(new { id, deleted = true }, httpContext));
            }
            catch (ListingAccessException ex)
            {
                return ToListingAccessResult(ex, httpContext);
            }
        })
        .WithName("DeleteListingV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/listings/{id:guid}/reserve", async (
            HttpContext httpContext,
            ListingStatusService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            try
            {
                var outcome = await service.ReserveAsync(id, ct);
                return ToStatusActionResult(outcome, id, httpContext);
            }
            catch (ListingAccessException ex)
            {
                return ToListingAccessResult(ex, httpContext);
            }
        })
        .WithName("ReserveListingV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/listings/{id:guid}/activate", async (
            HttpContext httpContext,
            ListingStatusService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            try
            {
                var outcome = await service.ActivateAsync(id, ct);
                return ToStatusActionResult(outcome, id, httpContext);
            }
            catch (ListingAccessException ex)
            {
                return ToListingAccessResult(ex, httpContext);
            }
        })
        .WithName("ActivateListingV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/listings/{id:guid}/sold", async (
            HttpContext httpContext,
            ListingStatusService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            try
            {
                var outcome = await service.MarkSoldAsync(id, ct);
                return ToStatusActionResult(outcome, id, httpContext);
            }
            catch (ListingAccessException ex)
            {
                return ToListingAccessResult(ex, httpContext);
            }
        })
        .WithName("MarkListingSoldV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/listings/{id:guid}/inactive", async (
            HttpContext httpContext,
            ListingStatusService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            try
            {
                var outcome = await service.SetInactiveAsync(id, ct);
                return ToStatusActionResult(outcome, id, httpContext);
            }
            catch (ListingAccessException ex)
            {
                return ToListingAccessResult(ex, httpContext);
            }
        })
        .WithName("SetListingInactiveV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/listings/{id:guid}/archive", async (
            HttpContext httpContext,
            ListingStatusService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            try
            {
                var outcome = await service.SetInactiveAsync(id, ct);
                return ToStatusActionResult(outcome, id, httpContext);
            }
            catch (ListingAccessException ex)
            {
                return ToListingAccessResult(ex, httpContext);
            }
        })
        .WithName("ArchiveListingV1")
        .WithSummary("已下架（與 Web Inactive 一致；舊名 archive 仍可用）")
        .RequireAuthorization();

        app.MapPatch("/api/v1/listings/{id:guid}/donated", async (
            HttpContext httpContext,
            ListingStatusService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            try
            {
                var outcome = await service.MarkDonatedAsync(id, ct);
                return ToStatusActionResult(outcome, id, httpContext);
            }
            catch (ListingAccessException ex)
            {
                return ToListingAccessResult(ex, httpContext);
            }
        })
        .WithName("MarkListingDonatedV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/listings/{id:guid}/given-or-traded", async (
            HttpContext httpContext,
            ListingStatusService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            try
            {
                var outcome = await service.MarkGivenOrTradedAsync(id, ct);
                return ToStatusActionResult(outcome, id, httpContext);
            }
            catch (ListingAccessException ex)
            {
                return ToListingAccessResult(ex, httpContext);
            }
        })
        .WithName("MarkListingGivenOrTradedV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/listings/{id:guid}/reactivate", async (
            HttpContext httpContext,
            ListingStatusService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            try
            {
                var outcome = await service.ReactivateAsync(id, ct);
                return ToStatusActionResult(outcome, id, httpContext);
            }
            catch (ListingAccessException ex)
            {
                return ToListingAccessResult(ex, httpContext);
            }
        })
        .WithName("ReactivateListingV1")
        .WithSummary("重新上架（僅已下架）")
        .RequireAuthorization();

        app.MapPost("/api/v1/listings/{id:guid}/top-pin", async (
            HttpContext httpContext,
            ListingTopPinService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            try
            {
                await service.UseTopPinAsync(id, ct);
                return Results.Ok(ApiResponseFactory.Success(new { id }, httpContext));
            }
            catch (ListingAccessException ex)
            {
                return ToListingAccessResult(ex, httpContext);
            }
        })
        .WithName("UseListingTopPinV1")
        .WithSummary("使用置頂次數（7 天）")
        .RequireAuthorization();

        app.MapDelete("/api/v1/listings/{id:guid}/top-pin", async (
            HttpContext httpContext,
            ListingTopPinService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            try
            {
                await service.EndTopPinAsync(id, ct);
                return Results.Ok(ApiResponseFactory.Success(new { id }, httpContext));
            }
            catch (ListingAccessException ex)
            {
                return ToListingAccessResult(ex, httpContext);
            }
        })
        .WithName("EndListingTopPinV1")
        .WithSummary("結束置頂（不退回次數）")
        .RequireAuthorization();

        app.MapPost("/api/v1/listings/{id:guid}/images", async (
            HttpContext httpContext,
            ListingCommandService service,
            Guid id,
            CancellationToken ct = default) =>
        {
            try
            {
                if (!httpContext.Request.HasFormContentType)
                {
                    return Results.BadRequest(ApiResponseFactory.Error("VALIDATION_ERROR", "需使用 multipart/form-data 上傳圖片", httpContext));
                }

                var form = await httpContext.Request.ReadFormAsync(ct);
                var file = form.Files.GetFile("file");
                if (file is null)
                {
                    return Results.BadRequest(ApiResponseFactory.Error("VALIDATION_ERROR", "缺少上傳檔案欄位 file", httpContext));
                }

                var result = await service.AddImageAsync(id, file, ct);
                return Results.Ok(ApiResponseFactory.Success(new
                {
                    imageId = result.ImageId,
                    sortOrder = result.SortOrder,
                    blobName = result.BlobName
                }, httpContext));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ApiResponseFactory.Error("VALIDATION_ERROR", ex.Message, httpContext));
            }
            catch (ListingAccessException ex)
            {
                return ToListingAccessResult(ex, httpContext);
            }
        })
        .WithName("UploadListingImageV1")
        .WithSummary("上傳商品圖片（multipart/form-data，欄位名稱 file）")
        .Accepts<IFormFile>("multipart/form-data")
        .RequireAuthorization();

        return app;
    }

    private static IResult ToListingAccessResult(ListingAccessException ex, HttpContext httpContext) =>
        Results.Json(
            ApiResponseFactory.Error(ex.Code, ex.Message, httpContext),
            statusCode: ex.StatusCode);

    private static IResult ToFavoriteErrorResult(HttpContext httpContext, string code, string message)
    {
        var statusCode = code switch
        {
            "LISTING_NOT_FOUND" => StatusCodes.Status404NotFound,
            "CATEGORY_NOT_FOUND" => StatusCodes.Status404NotFound,
            "LISTING_NOT_AVAILABLE" => StatusCodes.Status409Conflict,
            "LISTING_FAVORITE_OWN_LISTING_NOT_ALLOWED" => StatusCodes.Status409Conflict,
            "FORBIDDEN" => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Json(
            ApiResponseFactory.Error(code, message, httpContext),
            statusCode: statusCode);
    }

    private static IResult ToStatusActionResult(ListingStatusChangeOutcome outcome, Guid id, HttpContext httpContext)
    {
        return outcome.Result switch
        {
            ListingStatusChangeResult.Success =>
                Results.Ok(ApiResponseFactory.Success(
                    new { id, warning = outcome.Warning },
                    httpContext)),
            ListingStatusChangeResult.NotFound =>
                Results.NotFound(ApiResponseFactory.Error("LISTING_NOT_FOUND", "找不到商品", httpContext)),
            ListingStatusChangeResult.InvalidCurrentStatus =>
                Results.BadRequest(ApiResponseFactory.Error("VALIDATION_ERROR", "商品狀態資料無效", httpContext)),
            ListingStatusChangeResult.InvalidTransition =>
                Results.Conflict(ApiResponseFactory.Error("LISTING_INVALID_STATUS_TRANSITION", "目前狀態不可執行此操作", httpContext)),
            ListingStatusChangeResult.InvalidDonatedListingType =>
                Results.BadRequest(ApiResponseFactory.Error(
                    "LISTING_DONATED_NOT_APPLICABLE",
                    "此商品不是免費或愛心商品，無法標記為已捐贈",
                    httpContext)),
            ListingStatusChangeResult.InvalidTradeListingType =>
                Results.BadRequest(ApiResponseFactory.Error(
                    "LISTING_TRADE_MARKING_NOT_APPLICABLE",
                    "此商品未開啟以物易物，無法標記為已易物",
                    httpContext)),
            ListingStatusChangeResult.ReactivateInvalidState =>
                Results.BadRequest(ApiResponseFactory.Error(
                    "LISTING_REACTIVATE_NOT_ALLOWED",
                    "只有已下架的商品才能重新上架",
                    httpContext)),
            ListingStatusChangeResult.MaxActiveListingsReached =>
                Results.BadRequest(ApiResponseFactory.Error(
                    "LISTING_MAX_ACTIVE_REACHED",
                    $"您目前已有 {ListingConstants.MaxActiveListingsPerUser} 個刊登中的商品，請先下架或售出部分商品後再重新上架",
                    httpContext)),
            _ =>
                Results.BadRequest(ApiResponseFactory.Error("VALIDATION_ERROR", "狀態操作失敗", httpContext))
        };
    }
}
