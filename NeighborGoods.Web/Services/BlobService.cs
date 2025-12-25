using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using NeighborGoods.Web.Models.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace NeighborGoods.Web.Services;

/// <summary>
/// Azure Blob Storage 服務實作，用於處理商品圖片上傳與刪除。
/// </summary>
public class BlobService : IBlobService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public BlobService(AzureBlobOptions options)
    {
        if (string.IsNullOrEmpty(options.ConnectionString))
        {
            throw new InvalidOperationException("AzureBlob:ConnectionString 未設定。請在 appsettings.json 或 user-secrets 中設定。");
        }

        _blobServiceClient = new BlobServiceClient(options.ConnectionString);
        _containerName = options.ContainerName;
    }

    public async Task<string> UploadListingImageAsync(Guid listingId, Stream imageStream, string contentType, int sortOrder)
    {
        // 確保 Container 存在，並設為公開讀取（Blob 層級）
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

        // 壓縮圖片
        Stream compressedStream;
        try
        {
            compressedStream = await CompressImageAsync(imageStream);
        }
        catch (Exception)
        {
            // 如果壓縮失敗，使用原圖（降級處理）
            imageStream.Position = 0;
            compressedStream = imageStream;
        }

        // 產生唯一的檔名：統一使用 .jpg 格式
        var fileName = $"listings/{listingId}/{sortOrder}-{Guid.NewGuid()}.jpg";

        // 取得 Blob Client 並上傳
        var blobClient = containerClient.GetBlobClient(fileName);
        
        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "image/jpeg" // 統一為 JPG
            }
        };

        await blobClient.UploadAsync(compressedStream, uploadOptions);

        // 如果 compressedStream 是新的 MemoryStream，需要釋放
        if (compressedStream != imageStream && compressedStream is MemoryStream)
        {
            await compressedStream.DisposeAsync();
        }

        // 回傳完整的 Blob URL
        return blobClient.Uri.ToString();
    }

    /// <summary>
    /// 壓縮圖片：縮放至最大寬度 1200px，品質 70%，統一轉為 JPG。
    /// </summary>
    private async Task<Stream> CompressImageAsync(Stream imageStream)
    {
        const int maxWidth = 1200;
        const int quality = 70;

        // 載入圖片
        using var image = await Image.LoadAsync(imageStream);

        // 如果寬度超過 1200px，縮放至 1200px（保持比例）
        if (image.Width > maxWidth)
        {
            var ratio = (float)maxWidth / image.Width;
            var newHeight = (int)(image.Height * ratio);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(maxWidth, newHeight),
                Mode = ResizeMode.Max
            }));
        }

        // 建立 Jpeg 編碼器（品質 70%）
        var encoder = new JpegEncoder
        {
            Quality = quality
        };

        // 將壓縮後的圖片寫入 MemoryStream
        var compressedStream = new MemoryStream();
        await image.SaveAsync(compressedStream, encoder);
        compressedStream.Position = 0;

        return compressedStream;
    }

    public async Task DeleteListingImageAsync(string blobUrl)
    {
        if (string.IsNullOrEmpty(blobUrl))
        {
            return;
        }

        try
        {
            var uri = new Uri(blobUrl);
            
            // 從 URL 中提取 container 和 blob 名稱
            // 格式：https://{account}.blob.core.windows.net/{container}/{blob-path}
            var pathSegments = uri.AbsolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            if (pathSegments.Length < 2)
            {
                return; // URL 格式不正確
            }

            var containerName = pathSegments[0];
            var blobName = string.Join("/", pathSegments.Skip(1));

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            
            await blobClient.DeleteIfExistsAsync();
        }
        catch (Exception)
        {
            // 記錄錯誤但不拋出例外，避免影響其他操作
            // 可以考慮注入 ILogger 來記錄
        }
    }

    public async Task DeleteListingImagesAsync(List<string> blobUrls)
    {
        if (blobUrls == null || !blobUrls.Any())
        {
            return;
        }

        var tasks = blobUrls.Select(url => DeleteListingImageAsync(url));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 根據 Content-Type 取得副檔名。
    /// </summary>
    private static string GetExtensionFromContentType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            _ => ".jpg" // 預設使用 .jpg
        };
    }
}

