using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Admin.Contracts.Requests;
using NeighborGoods.Api.Features.Admin.Contracts.Responses;
using NeighborGoods.Api.Features.Admin.Services;
using NeighborGoods.Api.Features.Announcements.Contracts;
using NeighborGoods.Api.Features.Announcements.Services;
using NeighborGoods.Api.Features.Listing;
using NeighborGoods.Api.Infrastructure.Storage;
using NeighborGoods.Api.Shared.ApiContracts;
using NeighborGoods.Data;
using NeighborGoods.Data.Announcements;
using NeighborGoods.Api.Shared.Security;

namespace NeighborGoods.Api.Features.Admin;

public static class AdminEndpoints
{
    private const int AdminRoleCode = 3;
    private const int TopSubmissionPendingCode = 0;

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/dashboard", GetAdminDashboardAsync)
        .WithName("GetAdminDashboardV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/admin/listings/{id:guid}/force-status", ForceListingStatusAsync)
        .WithName("AdminForceListingStatusV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/admin/listings/batch-status", BatchForceListingStatusAsync)
        .WithName("AdminBatchForceListingStatusV1")
        .RequireAuthorization();

        app.MapDelete("/api/v1/admin/listings/{id:guid}/hard-delete", HardDeleteListingAsync)
        .WithName("AdminHardDeleteListingV1")
        .RequireAuthorization();

        app.MapGet("/api/v1/admin/listings", GetAdminListingsAsync)
        .WithName("AdminGetListingsV1")
        .RequireAuthorization();

        app.MapGet("/api/v1/admin/announcements", GetAdminAnnouncementsAsync)
        .WithName("AdminGetAnnouncementsV1")
        .RequireAuthorization();

        app.MapPost("/api/v1/admin/announcements", CreateAdminAnnouncementAsync)
        .WithName("AdminCreateAnnouncementV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/admin/announcements/{id:guid}", UpdateAdminAnnouncementAsync)
        .WithName("AdminUpdateAnnouncementV1")
        .RequireAuthorization();

        app.MapPatch("/api/v1/admin/announcements/{id:guid}/enabled", SetAdminAnnouncementEnabledAsync)
        .WithName("AdminSetAnnouncementEnabledV1")
        .RequireAuthorization();

        app.MapDelete("/api/v1/admin/announcements/{id:guid}", DeleteAdminAnnouncementAsync)
        .WithName("AdminDeleteAnnouncementV1")
        .RequireAuthorization();

        app.MapGet("/api/v1/admin/conversations", GetAdminConversationsAsync)
        .WithName("AdminGetConversationsV1")
        .RequireAuthorization();

        app.MapGet("/api/v1/admin/conversations/{conversationId:guid}/messages", GetAdminConversationMessagesAsync)
        .WithName("AdminGetConversationMessagesV1")
        .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> GetAdminDashboardAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUser, dbContext, ct);
        if (!isAdmin)
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var totalListings = await dbContext.Listings.AsNoTracking().CountAsync(ct);
        var activeListings = await dbContext.Listings.AsNoTracking().CountAsync(x => x.Status == (int)ListingStatus.Active, ct);
        var completedListings = await dbContext.Listings
            .AsNoTracking()
            .CountAsync(x => x.Status == (int)ListingStatus.Sold || x.Status == (int)ListingStatus.Donated || x.Status == (int)ListingStatus.GivenOrTraded, ct);
        var pendingTopSubmissions = await dbContext.ListingTopSubmissions.AsNoTracking().CountAsync(x => x.Status == TopSubmissionPendingCode, ct);
        var unreadAdminMessages = await dbContext.AdminMessages.AsNoTracking().CountAsync(x => !x.IsRead, ct);

        var latestListings = await dbContext.Listings
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new AdminDashboardListingResponse(x.Id, x.Title, x.Seller.DisplayName, x.Price, x.IsFree, x.Status, x.IsPinned, x.CreatedAt))
            .ToListAsync(ct);
        var latestMessages = await dbContext.AdminMessages
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new AdminDashboardMessageResponse(x.Id, x.Sender.DisplayName, x.Content, x.IsRead, x.CreatedAt))
            .ToListAsync(ct);
        var latestTopSubmissions = await dbContext.ListingTopSubmissions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new AdminDashboardTopSubmissionResponse(x.Id, x.User.DisplayName, x.ListingId, x.FeedbackTitle, x.Status, x.CreatedAt))
            .ToListAsync(ct);

        var payload = new AdminDashboardResponse(
            new AdminDashboardKpiResponse(totalListings, activeListings, completedListings, pendingTopSubmissions, unreadAdminMessages),
            latestListings,
            latestMessages,
            latestTopSubmissions);

        return Results.Ok(ApiResponseFactory.Success(payload, httpContext));
    }

    private static async Task<IResult> ForceListingStatusAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        Guid id,
        AdminForceListingStatusRequest request,
        CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUser, dbContext, ct);
        if (!isAdmin)
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!Enum.IsDefined(typeof(ListingStatus), request.Status))
        {
            return Results.BadRequest(ApiResponseFactory.Error("VALIDATION_ERROR", "無效的商品狀態代碼", httpContext));
        }

        var listing = await dbContext.Listings.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (listing is null)
        {
            return Results.NotFound(ApiResponseFactory.Error("LISTING_NOT_FOUND", "找不到商品", httpContext));
        }

        listing.Status = request.Status;
        listing.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return Results.Ok(ApiResponseFactory.Success(new { id, status = listing.Status, forced = true }, httpContext));
    }

    private static async Task<IResult> HardDeleteListingAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        IBlobStorage blobStorage,
        Guid id,
        CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUser, dbContext, ct);
        if (!isAdmin)
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var listingExists = await dbContext.Listings.AsNoTracking().AnyAsync(x => x.Id == id, ct);
        if (!listingExists)
        {
            return Results.NotFound(ApiResponseFactory.Error("LISTING_NOT_FOUND", "找不到商品", httpContext));
        }

        var imageBlobNames = await dbContext.ListingImages.Where(x => x.ListingId == id).Select(x => x.ImageUrl).ToListAsync(ct);
        var conversationIds = await dbContext.Conversations.Where(x => x.ListingId == id).Select(x => x.Id).ToListAsync(ct);

        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);
        await dbContext.Reviews.Where(x => x.ListingId == id).ExecuteDeleteAsync(ct);
        await dbContext.PurchaseRequests.Where(x => x.ListingId == id).ExecuteDeleteAsync(ct);
        if (conversationIds.Count > 0)
        {
            await dbContext.Messages.Where(x => conversationIds.Contains(x.ConversationId)).ExecuteDeleteAsync(ct);
            await dbContext.Conversations.Where(x => conversationIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
        }

        await dbContext.ListingFavorites.Where(x => x.ListingId == id).ExecuteDeleteAsync(ct);
        await dbContext.ListingTopSubmissions.Where(x => x.ListingId == id).ExecuteDeleteAsync(ct);
        await dbContext.ListingImages.Where(x => x.ListingId == id).ExecuteDeleteAsync(ct);
        await dbContext.Listings.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);

        foreach (var rawUrl in imageBlobNames)
        {
            var blobName = ListingBlobPath.ToBlobNameForDeletion(rawUrl);
            if (string.IsNullOrWhiteSpace(blobName))
            {
                continue;
            }

            try
            {
                await blobStorage.DeleteAsync(blobName, ct);
            }
            catch
            {
                // hard delete 以 DB 移除為主，blob 清理失敗不阻擋主流程
            }
        }

        return Results.Ok(ApiResponseFactory.Success(new { id, deleted = true, hardDeleted = true }, httpContext));
    }

    private static async Task<IResult> BatchForceListingStatusAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminBatchListingStatusRequest request,
        CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUser, dbContext, ct);
        if (!isAdmin)
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!Enum.IsDefined(typeof(ListingStatus), request.Status))
        {
            return Results.BadRequest(ApiResponseFactory.Error("VALIDATION_ERROR", "無效的商品狀態代碼", httpContext));
        }

        var ids = request.ListingIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            return Results.BadRequest(ApiResponseFactory.Error("VALIDATION_ERROR", "請提供至少一筆商品 ID", httpContext));
        }

        var count = await dbContext.Listings
            .Where(x => ids.Contains(x.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, request.Status)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);

        return Results.Ok(ApiResponseFactory.Success(new { updatedCount = count, status = request.Status }, httpContext));
    }

    private static async Task<IResult> GetAdminListingsAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        string? q = null,
        int? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUser, dbContext, ct);
        if (!isAdmin)
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.Listings.AsNoTracking().AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            query = query.Where(x =>
                EF.Functions.Like(x.Title, $"%{keyword}%") ||
                (x.Description != null && EF.Functions.Like(x.Description, $"%{keyword}%")) ||
                EF.Functions.Like(x.Seller.DisplayName, $"%{keyword}%"));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new AdminListingManagementItemResponse(
                x.Id,
                x.Title,
                x.Seller.DisplayName,
                Convert.ToInt32(x.Price),
                x.IsFree,
                x.Status,
                x.IsPinned,
                x.CreatedAt))
            .ToListAsync(ct);

        var payload = new AdminListingManagementResponse(
            items,
            new AdminListingManagementPaginationResponse(
                normalizedPage,
                normalizedPageSize,
                totalCount,
                (int)Math.Ceiling(totalCount / (double)normalizedPageSize)));

        return Results.Ok(ApiResponseFactory.Success(payload, httpContext));
    }

    private static async Task<IResult> GetAdminAnnouncementsAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        AnnouncementQueryService queryService,
        NeighborGoodsDbContext dbContext,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(currentUser, dbContext, ct))
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var items = await queryService.GetAllForAdminAsync(ct);
        return Results.Ok(ApiResponseFactory.Success(new AdminAnnouncementsListResponse(items), httpContext));
    }

    private static async Task<IResult> CreateAdminAnnouncementAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        UpsertAnnouncementRequest request,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(currentUser, dbContext, ct))
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var validation = AnnouncementValidation.ValidateWrite(
            request.Message,
            request.Severity,
            request.Scope,
            request.StartsAt,
            request.EndsAt,
            request.LinkUrl);
        if (!validation.IsValid)
        {
            return Results.BadRequest(ApiResponseFactory.Error(validation.ErrorCode!, validation.ErrorMessage!, httpContext));
        }

        var userId = currentUser.GetRequiredUserId();
        var now = DateTime.UtcNow;
        var entity = new SiteAnnouncement
        {
            Id = Guid.NewGuid(),
            Message = request.Message.Trim(),
            Severity = request.Severity,
            Scope = request.Scope,
            SortOrder = request.SortOrder,
            IsEnabled = request.IsEnabled,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            LinkUrl = string.IsNullOrWhiteSpace(request.LinkUrl) ? null : request.LinkUrl.Trim(),
            LinkLabel = string.IsNullOrWhiteSpace(request.LinkLabel) ? null : request.LinkLabel.Trim(),
            CreatedAt = now,
            CreatedByUserId = userId,
        };

        dbContext.SiteAnnouncements.Add(entity);
        await dbContext.SaveChangesAsync(ct);

        return Results.Ok(ApiResponseFactory.Success(ToAdminResponse(entity), httpContext));
    }

    private static async Task<IResult> UpdateAdminAnnouncementAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        Guid id,
        UpsertAnnouncementRequest request,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(currentUser, dbContext, ct))
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var validation = AnnouncementValidation.ValidateWrite(
            request.Message,
            request.Severity,
            request.Scope,
            request.StartsAt,
            request.EndsAt,
            request.LinkUrl);
        if (!validation.IsValid)
        {
            return Results.BadRequest(ApiResponseFactory.Error(validation.ErrorCode!, validation.ErrorMessage!, httpContext));
        }

        var entity = await dbContext.SiteAnnouncements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            return Results.NotFound(ApiResponseFactory.Error("ANNOUNCEMENT_NOT_FOUND", "找不到公告", httpContext));
        }

        var userId = currentUser.GetRequiredUserId();
        entity.Message = request.Message.Trim();
        entity.Severity = request.Severity;
        entity.Scope = request.Scope;
        entity.SortOrder = request.SortOrder;
        entity.IsEnabled = request.IsEnabled;
        entity.StartsAt = request.StartsAt;
        entity.EndsAt = request.EndsAt;
        entity.LinkUrl = string.IsNullOrWhiteSpace(request.LinkUrl) ? null : request.LinkUrl.Trim();
        entity.LinkLabel = string.IsNullOrWhiteSpace(request.LinkLabel) ? null : request.LinkLabel.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = userId;

        await dbContext.SaveChangesAsync(ct);
        return Results.Ok(ApiResponseFactory.Success(ToAdminResponse(entity), httpContext));
    }

    private static async Task<IResult> SetAdminAnnouncementEnabledAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        Guid id,
        SetAnnouncementEnabledRequest request,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(currentUser, dbContext, ct))
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var entity = await dbContext.SiteAnnouncements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            return Results.NotFound(ApiResponseFactory.Error("ANNOUNCEMENT_NOT_FOUND", "找不到公告", httpContext));
        }

        entity.IsEnabled = request.IsEnabled;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = currentUser.GetRequiredUserId();
        await dbContext.SaveChangesAsync(ct);

        return Results.Ok(ApiResponseFactory.Success(ToAdminResponse(entity), httpContext));
    }

    private static async Task<IResult> DeleteAdminAnnouncementAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        Guid id,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(currentUser, dbContext, ct))
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var entity = await dbContext.SiteAnnouncements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            return Results.NotFound(ApiResponseFactory.Error("ANNOUNCEMENT_NOT_FOUND", "找不到公告", httpContext));
        }

        dbContext.SiteAnnouncements.Remove(entity);
        await dbContext.SaveChangesAsync(ct);

        return Results.Ok(ApiResponseFactory.Success(new { id, deleted = true }, httpContext));
    }

    private static async Task<IResult> GetAdminConversationsAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminConversationQueryService conversationQueryService,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUser, dbContext, ct);
        if (!isAdmin)
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var result = await conversationQueryService.ListConversationsAsync(page, pageSize, ct);
        return Results.Ok(ApiResponseFactory.Success(result, httpContext));
    }

    private static async Task<IResult> GetAdminConversationMessagesAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminConversationQueryService conversationQueryService,
        Guid conversationId,
        int page = 1,
        int pageSize = 100,
        CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUser, dbContext, ct);
        if (!isAdmin)
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var (data, conversationExists) = await conversationQueryService.GetMessagesAsync(
            conversationId,
            page,
            pageSize,
            ct);

        if (!conversationExists)
        {
            return Results.Json(
                ApiResponseFactory.Error("CONVERSATION_NOT_FOUND", "找不到對話", httpContext),
                statusCode: StatusCodes.Status404NotFound);
        }

        return Results.Ok(ApiResponseFactory.Success(data, httpContext));
    }

    private static AdminAnnouncementResponse ToAdminResponse(SiteAnnouncement entity) =>
        new(
            entity.Id,
            entity.Message,
            entity.Severity,
            entity.Scope,
            entity.SortOrder,
            entity.IsEnabled,
            entity.StartsAt,
            entity.EndsAt,
            entity.LinkUrl,
            entity.LinkLabel,
            entity.CreatedAt,
            entity.CreatedByUserId,
            entity.UpdatedAt,
            entity.UpdatedByUserId);

    private static async Task<bool> IsAdminAsync(
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        CancellationToken ct)
    {
        var userId = currentUser.GetRequiredUserId();
        return await dbContext.AspNetUsers
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId && x.Role == AdminRoleCode, ct);
    }
}
