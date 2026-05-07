using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Admin.Contracts.Responses;
using NeighborGoods.Api.Features.Listing;
using NeighborGoods.Api.Shared.ApiContracts;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Security;

namespace NeighborGoods.Api.Features.Admin;

public static class AdminEndpoints
{
    private const int AdminRoleCode = 3;
    private const int TopSubmissionPendingCode = 0;

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/dashboard", async (
            HttpContext httpContext,
            ICurrentUserContext currentUser,
            NeighborGoodsDbContext dbContext,
            CancellationToken ct = default) =>
        {
            var userId = currentUser.GetRequiredUserId();
            var isAdmin = await dbContext.AspNetUsers
                .AsNoTracking()
                .AnyAsync(x => x.Id == userId && x.Role == AdminRoleCode, ct);
            if (!isAdmin)
            {
                return Results.Json(
                    ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var totalListingsTask = dbContext.Listings
                .AsNoTracking()
                .CountAsync(ct);
            var activeListingsTask = dbContext.Listings
                .AsNoTracking()
                .CountAsync(x => x.Status == (int)ListingStatus.Active, ct);
            var completedListingsTask = dbContext.Listings
                .AsNoTracking()
                .CountAsync(x =>
                    x.Status == (int)ListingStatus.Sold ||
                    x.Status == (int)ListingStatus.Donated ||
                    x.Status == (int)ListingStatus.GivenOrTraded, ct);
            var pendingTopSubmissionsTask = dbContext.ListingTopSubmissions
                .AsNoTracking()
                .CountAsync(x => x.Status == TopSubmissionPendingCode, ct);
            var unreadAdminMessagesTask = dbContext.AdminMessages
                .AsNoTracking()
                .CountAsync(x => !x.IsRead, ct);

            var latestListingsTask = dbContext.Listings
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .Select(x => new AdminDashboardListingResponse(
                    x.Id,
                    x.Title,
                    x.Seller.DisplayName,
                    x.Price,
                    x.IsFree,
                    x.Status,
                    x.IsPinned,
                    x.CreatedAt))
                .ToListAsync(ct);

            var latestMessagesTask = dbContext.AdminMessages
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .Select(x => new AdminDashboardMessageResponse(
                    x.Id,
                    x.Sender.DisplayName,
                    x.Content,
                    x.IsRead,
                    x.CreatedAt))
                .ToListAsync(ct);

            var latestTopSubmissionsTask = dbContext.ListingTopSubmissions
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .Select(x => new AdminDashboardTopSubmissionResponse(
                    x.Id,
                    x.User.DisplayName,
                    x.ListingId,
                    x.FeedbackTitle,
                    x.Status,
                    x.CreatedAt))
                .ToListAsync(ct);

            await Task.WhenAll(
                totalListingsTask,
                activeListingsTask,
                completedListingsTask,
                pendingTopSubmissionsTask,
                unreadAdminMessagesTask,
                latestListingsTask,
                latestMessagesTask,
                latestTopSubmissionsTask);

            var payload = new AdminDashboardResponse(
                new AdminDashboardKpiResponse(
                    totalListingsTask.Result,
                    activeListingsTask.Result,
                    completedListingsTask.Result,
                    pendingTopSubmissionsTask.Result,
                    unreadAdminMessagesTask.Result),
                latestListingsTask.Result,
                latestMessagesTask.Result,
                latestTopSubmissionsTask.Result);

            return Results.Ok(ApiResponseFactory.Success(payload, httpContext));
        })
        .WithName("GetAdminDashboardV1")
        .RequireAuthorization();

        return app;
    }
}
