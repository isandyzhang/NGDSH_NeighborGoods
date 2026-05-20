using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Announcements.Contracts;
using NeighborGoods.Data;
using NeighborGoods.Data.Announcements;

namespace NeighborGoods.Api.Features.Announcements.Services;

public sealed class AnnouncementQueryService(NeighborGoodsDbContext dbContext)
{
    public async Task<IReadOnlyList<ActiveAnnouncementItemResponse>> GetActiveAsync(
        byte? scope,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var query = dbContext.SiteAnnouncements
            .AsNoTracking()
            .Where(x => x.IsEnabled
                && (x.StartsAt == null || x.StartsAt <= now)
                && (x.EndsAt == null || x.EndsAt > now));

        if (scope.HasValue)
        {
            var scopeValue = scope.Value;
            query = query.Where(x => x.Scope == (byte)AnnouncementScope.Global || x.Scope == scopeValue);
        }

        return await query
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new ActiveAnnouncementItemResponse(
                x.Id,
                x.Message,
                x.Severity,
                x.Scope,
                x.LinkUrl,
                x.LinkLabel))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AdminAnnouncementResponse>> GetAllForAdminAsync(CancellationToken ct = default)
    {
        return await dbContext.SiteAnnouncements
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new AdminAnnouncementResponse(
                x.Id,
                x.Message,
                x.Severity,
                x.Scope,
                x.SortOrder,
                x.IsEnabled,
                x.StartsAt,
                x.EndsAt,
                x.LinkUrl,
                x.LinkLabel,
                x.CreatedAt,
                x.CreatedByUserId,
                x.UpdatedAt,
                x.UpdatedByUserId))
            .ToListAsync(ct);
    }
}
