namespace NeighborGoods.Notifications;

/// <summary>組 LINE 內開啟站內路徑的 LIFF 深連結（liff.state）；LIFF Endpoint 須為網站根 /。</summary>
public static class LineLiffUrlBuilder
{
    public static string BuildDeepLink(string liffId, string internalPath)
    {
        if (string.IsNullOrWhiteSpace(liffId))
        {
            throw new ArgumentException("liffId is required", nameof(liffId));
        }

        var path = NormalizePath(internalPath);
        return $"https://liff.line.me/{liffId.Trim()}?liff.state={Uri.EscapeDataString(path)}";
    }

    public static string BuildAppUrl(string webBaseUrl, string internalPath)
    {
        var baseUrl = string.IsNullOrWhiteSpace(webBaseUrl)
            ? "http://localhost:5173"
            : webBaseUrl.TrimEnd('/');
        return $"{baseUrl}{NormalizePath(internalPath)}";
    }

    /// <summary>已設定 LiffId 時回傳 LIFF 深連結，否則回傳一般 HTTPS 站內連結。</summary>
    public static string BuildLineOpenUrl(LineMessagingOptions options, string internalPath)
    {
        if (!string.IsNullOrWhiteSpace(options.LiffId))
        {
            return BuildDeepLink(options.LiffId, internalPath);
        }

        return BuildAppUrl(options.WebBaseUrl, internalPath);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        return path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
    }
}
