using NeighborGoods.Data.Line;

namespace NeighborGoods.Workers.Line;

public interface ILineTransactionGroupService
{
    Task RegisterFromWebhookAsync(
        string groupId,
        string eventType,
        CancellationToken cancellationToken = default);

    Task MarkLeftAsync(string groupId, CancellationToken cancellationToken = default);

    Task<LineTransactionGroupSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task<LineTransactionGroup?> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<bool> ActivateAsync(Guid id, CancellationToken cancellationToken = default);
}
