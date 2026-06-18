namespace NeighborGoods.Workers.Listings;

internal static class ListingImageUrlHelper
{
    public static string? Resolve(string? storedPathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(storedPathOrUrl))
        {
            return null;
        }

        if (storedPathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || storedPathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return storedPathOrUrl;
        }

        return null;
    }
}
