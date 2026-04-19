namespace NeighborGoods.Api.Features.Listing;

/// <summary>將 DB 內的 ImageUrl（blob 名稱或歷史完整 URL）轉成可送進 Blob Delete 的路徑。</summary>
public static class ListingBlobPath
{
    public static string ToBlobNameForDeletion(string storedImageUrl)
    {
        var s = storedImageUrl.Trim();
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        if (!s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return s.TrimStart('/');
        }

        foreach (var marker in new[] { "/listing/", "/listings/" })
        {
            var idx = s.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return s[(idx + marker.Length)..].TrimStart('/');
            }
        }

        return s;
    }

    public static bool StoredImageMatchesDeleteToken(string storedRaw, string deleteToken, Func<string, string> resolvePublicUrl)
    {
        var a = storedRaw.Trim();
        var b = deleteToken.Trim();
        if (string.Equals(a, b, StringComparison.Ordinal))
        {
            return true;
        }

        var resolved = resolvePublicUrl(a);
        return string.Equals(resolved, b, StringComparison.Ordinal);
    }
}
