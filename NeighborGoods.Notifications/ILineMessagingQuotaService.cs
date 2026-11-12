namespace NeighborGoods.Notifications;

public interface ILineMessagingQuotaService
{
    Task<LineMessagingQuotaStatus> GetCurrentQuotaAsync(CancellationToken cancellationToken = default);

    Task<int?> GetGroupMemberCountAsync(string groupId, CancellationToken cancellationToken = default);
}
