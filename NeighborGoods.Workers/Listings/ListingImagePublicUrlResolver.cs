using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using NeighborGoods.Workers.Storage;

namespace NeighborGoods.Workers.Listings;

/// <summary>
/// 將 DB 內相對 blob 路徑轉成公開 HTTPS URL（與 Api BuildPublicUrl 行為對齊），供 LINE Flex 使用。
/// </summary>
public sealed class ListingImagePublicUrlResolver
{
    private readonly BlobContainerClient? _containerClient;

    public ListingImagePublicUrlResolver(IOptions<AzureBlobOptions> options)
    {
        var value = options.Value;
        if (string.IsNullOrWhiteSpace(value.ConnectionString) ||
            string.IsNullOrWhiteSpace(value.ContainerName))
        {
            return;
        }

        _containerClient = new BlobServiceClient(value.ConnectionString)
            .GetBlobContainerClient(value.ContainerName);
    }

    public string? Resolve(string? storedPathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(storedPathOrUrl))
        {
            return null;
        }

        if (storedPathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            storedPathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return storedPathOrUrl;
        }

        if (_containerClient is null)
        {
            return null;
        }

        return _containerClient.GetBlobClient(storedPathOrUrl).Uri.ToString();
    }
}
