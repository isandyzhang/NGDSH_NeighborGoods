namespace NeighborGoods.Web.Utils;

/// <summary>
/// 取得台灣時間（UTC+8），方便統一使用。
/// </summary>
public static class TaiwanTime
{
    private static readonly TimeZoneInfo TaiwanTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time");

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TaiwanTimeZone);
}


