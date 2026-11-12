using NeighborGoods.Notifications;

namespace NeighborGoods.Api.Tests;

internal sealed class FakeLineMessagingQuotaService : ILineMessagingQuotaService
{
    public int MemberCount { get; set; } = 3;
    public int RemainingCount { get; set; } = 1000;
    public bool ReturnNullMemberCount { get; set; }

    public Task<LineMessagingQuotaStatus> GetCurrentQuotaAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new LineMessagingQuotaStatus(
            IsEstimated: true,
            PlanType: "limited",
            MonthlyQuota: RemainingCount + 10,
            UsedCount: 10,
            RemainingCount: RemainingCount,
            UsagePercent: 1,
            Note: "test fake"));
    }

    public Task<int?> GetGroupMemberCountAsync(string groupId, CancellationToken cancellationToken = default)
    {
        if (ReturnNullMemberCount)
        {
            return Task.FromResult<int?>(null);
        }

        return Task.FromResult<int?>(MemberCount);
    }
}
