namespace NeighborGoods.Notifications;

/// <summary>組 LINE 內開啟站內路徑的 LIFF 連結；LIFF Endpoint 須為網站根 /。</summary>
public static class LineLiffUrlBuilder
{
    /// <summary>path 格式：liff.line.me/{liffId}/account（圖文選單、Flex 按鈕等，手機實測較穩）。</summary>
    public static string BuildPathLink(string liffId, string internalPath, string? query = null)
    {
        if (string.IsNullOrWhiteSpace(liffId))
        {
            throw new ArgumentException("liffId is required", nameof(liffId));
        }

        var path = NormalizePath(internalPath);
        var normalizedQuery = NormalizeQuery(query);
        return $"https://liff.line.me/{liffId.Trim()}{path}{normalizedQuery}";
    }

    /// <summary>綁定流程：根路徑 + 外層 bindToken/botLink（須在 / 完成 liff.init）。</summary>
    public static string BuildBindingUrl(string liffId, string bindToken, string botLink)
    {
        var query =
            $"?bindToken={Uri.EscapeDataString(bindToken.Trim())}" +
            $"&botLink={Uri.EscapeDataString(botLink.Trim())}";
        return BuildPathLink(liffId, "/", query);
    }

    /// <summary>liff.state 深連結（僅保留需 query-only 旗標的舊流程）。</summary>
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
            return BuildPathLink(options.LiffId, internalPath, query);
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

        return query.StartsWith('?') ? query : "?" + query;
    }
}
