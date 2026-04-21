using Microsoft.Extensions.Options;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Shared.Notifications;

public sealed class LinePushPolicyService(
    IOptions<LineMessagingOptions> options,
    LinePushQuotaTracker quotaTracker)
{
    private readonly LineMessagingOptions _options = options.Value;

    public bool CanSendTransactionalPush(AspNetUser user, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(user.LineMessagingApiUserId))
        {
            return false;
        }

        if (!user.LineMessagingApiAuthorizedAt.HasValue)
        {
            return false;
        }

        var cooldownMinutes = Math.Max(0, _options.PushCooldownMinutes);
        if (cooldownMinutes == 0)
        {
            return true;
        }

        if (!user.LineNotificationLastSentAt.HasValue)
        {
            return true;
        }

        return nowUtc - user.LineNotificationLastSentAt.Value >= TimeSpan.FromMinutes(cooldownMinutes);
    }

    public bool CanSendPreferencePush(AspNetUser user, LineNotificationPreferenceFlags category, DateTime nowUtc)
    {
        if (!_options.PreferencePushEnabled)
        {
            return false;
        }

        if (!CanSendTransactionalPush(user, nowUtc))
        {
            return false;
        }

        var currentPercent = GetCurrentUsagePercent();
        if (currentPercent >= Math.Max(1, _options.PreferencePushSafetyUsagePercent))
        {
            return false;
        }

        var preference = (LineNotificationPreferenceFlags)user.LineNotificationPreference;
        if (preference == LineNotificationPreferenceFlags.None)
        {
            return false;
        }

        return preference.HasFlag(category);
    }

    public int GetCurrentUsagePercent()
    {
        var quota = Math.Max(1, _options.MonthlyPushQuota);
        var used = quotaTracker.GetCurrentMonthPushCount();
        return (int)Math.Round((used * 100.0) / quota, MidpointRounding.AwayFromZero);
    }
}
