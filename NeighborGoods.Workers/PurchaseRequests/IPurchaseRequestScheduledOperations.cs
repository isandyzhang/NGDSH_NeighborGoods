namespace NeighborGoods.Workers.PurchaseRequests;

public interface IPurchaseRequestScheduledOperations
{
    Task<int> SendSellerReminderAsync(CancellationToken cancellationToken = default);

    Task<int> ExpirePendingAsync(CancellationToken cancellationToken = default);
}
