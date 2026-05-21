namespace NeighborGoods.Notifications;

/// <summary>組 LINE 內開啟站內路徑的 LIFF 連結；LIFF Endpoint 須為網站根 /。</summary>
public static class LineLiffUrlBuilder
{
    /// <summary>
    /// Path 格式：liff.line.me/{liffId}/account（圖文選單、Flex 按鈕；手機實測較穩）。
    /// query 可為 "?from=listings" 或 "bindToken=...&botLink=..."（勿含前導 ? 亦可）。
    /// </summary>
    public static string BuildPathLiffUrl(string liffId, string internalPath, string? query = null)
    {
        if (string.IsNullOrWhiteSpace(liffId))
        {
            throw new ArgumentException("liffId is required", nameof(liffId));
        }

        var path = NormalizePath(internalPath);
        var normalizedQuery = NormalizeQuery(query);
        return $"https://liff.line.me/{liffId.Trim()}{path}{normalizedQuery}";
    }

    /// <summary>舊版 liff.state 深連結；僅保留給需相容的場合，新程式請用 BuildPathLiffUrl。</summary>
    public static string BuildDeepLink(string liffId, string internalPath)
    {
        if (string.IsNullOrWhiteSpace(liffId))
        {
            throw new ArgumentException("liffId is required", nameof(liffId));
        }

        var path = NormalizePath(internalPath);
        return $"https://liff.line.me/{liffId.Trim()}?liff.state={Uri.EscapeDataString(path)}";
    }

    public static string BuildAppUrl(string webBaseUrl, string internalPath, string? query = null)
    {
        var baseUrl = string.IsNullOrWhiteSpace(webBaseUrl)
            ? "http://localhost:5173"
            : webBaseUrl.TrimEnd('/');
        var path = NormalizePath(internalPath);
        var normalizedQuery = NormalizeQuery(query);
        return $"{baseUrl}{path}{normalizedQuery}";
    }

    /// <summary>已設定 LiffId 時回傳 path 型 LIFF 連結，否則回傳一般 HTTPS 站內連結。</summary>
    public static string BuildLineOpenUrl(LineMessagingOptions options, string internalPath, string? query = null)
    {
        if (!string.IsNullOrWhiteSpace(options.LiffId))
        {
            return BuildPathLiffUrl(options.LiffId, internalPath, query);
        }

        return BuildAppUrl(options.WebBaseUrl, internalPath, query);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        return path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
    }

    private static string NormalizeQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        var trimmed = query.Trim();
        return trimmed.StartsWith('?') ? trimmed : "?" + trimmed;
    }
}
