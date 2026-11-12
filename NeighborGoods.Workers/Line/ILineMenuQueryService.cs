using NeighborGoods.Notifications;

namespace NeighborGoods.Workers.Line;

public interface ILineMenuQueryService
{
    Task<LineBoundUser?> GetBoundUserAsync(string lineUserId, CancellationToken cancellationToken = default);

    Task<LineMyListingsSummary> GetMyListingsSummaryAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LineMyListingCardItem>> GetMyListingCardItemsAsync(
        string userId,
        int maxItems = 5,
        CancellationToken cancellationToken = default);

    Task<LineMyMessagesSummary> GetMyMessagesSummaryAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
