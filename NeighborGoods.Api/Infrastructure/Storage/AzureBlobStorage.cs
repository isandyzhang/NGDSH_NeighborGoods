using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace NeighborGoods.Api.Infrastructure.Storage;

public sealed class AzureBlobStorage(
    IOptions<AzureBlobOptions> options,
    ILogger<AzureBlobStorage> logger) : IBlobStorage
{
    private readonly BlobContainerClient _containerClient = CreateContainerClient(options.Value);

    public async Task<string> UploadCompressedJpegAsync(
        string blobName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        var blobClient = _containerClient.GetBlobClient(blobName);

        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        await using var sourceBuffer = new MemoryStream();
        await content.CopyToAsync(sourceBuffer, cancellationToken);
        sourceBuffer.Position = 0;

        Stream uploadStream = sourceBuffer;
        try
        {
            uploadStream = await CompressImageAsync(sourceBuffer, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Image compression failed. Fallback to original stream.");
            sourceBuffer.Position = 0;
            uploadStream = sourceBuffer;
        }

        try
        {
            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "image/jpeg"
                }
            };

            await blobClient.UploadAsync(uploadStream, uploadOptions, cancellationToken);
        }
        finally
        {
            if (!ReferenceEquals(uploadStream, sourceBuffer))
            {
                await uploadStream.DisposeAsync();
            }
        }

        return blobName;
    }

    public async Task DeleteAsync(string blobName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return;
        }

        var blobClient = _containerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public string BuildPublicUrl(string blobName)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return string.Empty;
        }

        return _containerClient.GetBlobClient(blobName).Uri.ToString();
    }

    private static BlobContainerClient CreateContainerClient(AzureBlobOptions blobOptions)
    {
        if (string.IsNullOrWhiteSpace(blobOptions.ConnectionString))
        {
            throw new InvalidOperationException("AzureBlob:ConnectionString is required.");
        }

        var serviceClient = new BlobServiceClient(blobOptions.ConnectionString);
        return serviceClient.GetBlobContainerClient(blobOptions.ContainerName);
    }

    private static async Task<Stream> CompressImageAsync(Stream source, CancellationToken cancellationToken)
    {
        const int maxWidth = 1200;
        const int quality = 70;

        source.Position = 0;
        using var image = await Image.LoadAsync(source, cancellationToken);

        if (image.Width > maxWidth)
        {
            var ratio = (float)maxWidth / image.Width;
            var targetHeight = (int)(image.Height * ratio);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(maxWidth, targetHeight),
                Mode = ResizeMode.Max
            }));
        }

        var output = new MemoryStream();
        await image.SaveAsync(output, new JpegEncoder { Quality = quality }, cancellationToken);
        output.Position = 0;
        return output;
    }
}
