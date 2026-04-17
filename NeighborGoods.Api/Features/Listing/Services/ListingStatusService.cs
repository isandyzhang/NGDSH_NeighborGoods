using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Shared.Persistence;

namespace NeighborGoods.Api.Features.Listing.Services;

public sealed class ListingStatusService(NeighborGoodsDbContext dbContext)
{
    public Task<ListingStatusChangeResult> ReserveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return ChangeStatusAsync(id, ListingStatus.Reserved, cancellationToken);
    }

    public Task<ListingStatusChangeResult> ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return ChangeStatusAsync(id, ListingStatus.Active, cancellationToken);
    }

    public Task<ListingStatusChangeResult> MarkSoldAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return ChangeStatusAsync(id, ListingStatus.Sold, cancellationToken);
    }

    public Task<ListingStatusChangeResult> ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return ChangeStatusAsync(id, ListingStatus.Archived, cancellationToken);
    }

    private async Task<ListingStatusChangeResult> ChangeStatusAsync(
        Guid id,
        ListingStatus targetStatus,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Listings
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return ListingStatusChangeResult.NotFound;
        }

        if (!ListingStatusRules.IsValid(entity.Status))
        {
            return ListingStatusChangeResult.InvalidCurrentStatus;
        }

        var fromStatus = (ListingStatus)entity.Status;
        if (!ListingStatusRules.CanTransition(fromStatus, targetStatus))
        {
            return ListingStatusChangeResult.InvalidTransition;
        }

        entity.Status = (int)targetStatus;
        entity.UpdatedAt = DateTime.UtcNow;

        dbContext.Listings.Update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ListingStatusChangeResult.Success;
    }
}
