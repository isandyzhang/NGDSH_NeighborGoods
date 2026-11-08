namespace NeighborGoods.Notifications;

public sealed record LinePushAttemptResult(
    bool Success,
    string Code,
    string Message,
    int? HttpStatusCode = null,
    string? ProviderResponse = null)
{
    public static LinePushAttemptResult Ok() =>
        new(true, "OK", "LINE 推播成功");

    public static LinePushAttemptResult MissingToken() =>
        new(false, "TOKEN_MISSING", "未設定 LineMessagingApi:ChannelAccessToken");

    public static LinePushAttemptResult MissingLineUserId() =>
        new(false, "LINE_USER_MISSING", "缺少 LINE Messaging API user id");

    public static LinePushAttemptResult ProviderFailed(int statusCode, string response) =>
        new(false, "LINE_API_FAILED", $"LINE API 回傳失敗（HTTP {statusCode}）", statusCode, response);

    public static LinePushAttemptResult Exception(string message) =>
        new(false, "LINE_EXCEPTION", message);
}
