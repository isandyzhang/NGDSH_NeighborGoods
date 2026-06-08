namespace NeighborGoods.Api.Features.Listing.Services;

public static class SellerLoginActivityResolver
{
    public static SellerLoginActivity Resolve(DateTime? lastLoginAt, DateTime nowUtc)
    {
        if (!lastLoginAt.HasValue)
        {
            return new SellerLoginActivity("unknown", "尚無登入紀錄");
        }

        var elapsed = nowUtc - lastLoginAt.Value;
        if (elapsed < TimeSpan.FromHours(1))
        {
            return new SellerLoginActivity("recent", "賣家 1 小時內有登入");
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            return new SellerLoginActivity("today", "賣家今日有登入");
        }

        if (elapsed < TimeSpan.FromDays(7))
        {
            return new SellerLoginActivity("week", "賣家本週有登入");
        }

        if (elapsed < TimeSpan.FromDays(30))
        {
            return new SellerLoginActivity("inactive", "賣家超過一週未登入");
        }

        var weeks = Math.Max(1, (int)Math.Round(elapsed.TotalDays / 7d, MidpointRounding.AwayFromZero));
        return new SellerLoginActivity("stale", $"賣家已 {weeks} 週未登入");
    }
}

public sealed record SellerLoginActivity(string Level, string Label);
