using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeighborGoods.Data;
using NeighborGoods.Data.Line;

namespace NeighborGoods.Workers.Line;

public sealed class LineTransactionGroupService(
    NeighborGoodsDbContext dbContext,
    ILogger<LineTransactionGroupService> logger) : ILineTransactionGroupService
{
    public async Task RegisterFromWebhookAsync(
        string groupId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return;
        }

        var normalizedGroupId = groupId.Trim();
        var now = DateTime.UtcNow;
        var existing = await dbContext.LineTransactionGroups
            .FirstOrDefaultAsync(x => x.GroupId == normalizedGroupId, cancellationToken);

        if (existing is null)
        {
            var hasActive = await dbContext.LineTransactionGroups
                .AnyAsync(x => x.IsActive, cancellationToken);

            var entity = new LineTransactionGroup
            {
                Id = Guid.NewGuid(),
                GroupId = normalizedGroupId,
                IsActive = !hasActive,
                JoinedAt = now,
                LeftAt = null
            };
            dbContext.LineTransactionGroups.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "LINE transaction group registered: groupId={GroupId} isActive={IsActive} eventType={EventType}",
                normalizedGroupId,
                entity.IsActive,
                eventType);
            return;
        }

        var changed = false;
        if (existing.LeftAt is not null)
        {
            existing.LeftAt = null;
            changed = true;
        }

        if (!existing.IsActive)
        {
            var hasOtherActive = await dbContext.LineTransactionGroups
                .AnyAsync(x => x.IsActive && x.Id != existing.Id, cancellationToken);
            if (!hasOtherActive)
            {
                existing.IsActive = true;
                existing.JoinedAt = now;
                changed = true;
            }
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "LINE transaction group updated from webhook: groupId={GroupId} isActive={IsActive} eventType={EventType}",
                normalizedGroupId,
                existing.IsActive,
                eventType);
        }
    }

    public async Task MarkLeftAsync(string groupId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return;
        }

        var normalizedGroupId = groupId.Trim();
        var existing = await dbContext.LineTransactionGroups
            .FirstOrDefaultAsync(x => x.GroupId == normalizedGroupId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        if (!existing.IsActive && existing.LeftAt is not null)
        {
            return;
        }

        existing.IsActive = false;
        existing.LeftAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("LINE transaction group left: groupId={GroupId}", normalizedGroupId);
    }

    public async Task<LineTransactionGroupSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var groups = await dbContext.LineTransactionGroups
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.JoinedAt)
            .ToListAsync(cancellationToken);

        var active = groups.FirstOrDefault(x => x.IsActive);
        var inactive = groups.Where(x => !x.IsActive).ToList();
        return new LineTransactionGroupSnapshot(active, inactive);
    }

    public async Task<LineTransactionGroup?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.LineTransactionGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsActive, cancellationToken);
    }

    public async Task<bool> ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var target = await dbContext.LineTransactionGroups
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (target is null)
        {
            return false;
        }

        var actives = await dbContext.LineTransactionGroups
            .Where(x => x.IsActive && x.Id != id)
            .ToListAsync(cancellationToken);
        foreach (var active in actives)
        {
            active.IsActive = false;
        }

        target.IsActive = true;
        target.LeftAt = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed record LineTransactionGroupSnapshot(
    LineTransactionGroup? Active,
    IReadOnlyList<LineTransactionGroup> Inactive);
