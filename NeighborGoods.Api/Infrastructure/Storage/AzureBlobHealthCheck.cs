using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace NeighborGoods.Api.Infrastructure.Storage;

public sealed class AzureBlobHealthCheck(IOptions<AzureBlobOptions> options) : IHealthCheck
{
    private readonly AzureBlobOptions _options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            return HealthCheckResult.Unhealthy("AzureBlob:ConnectionString is missing.");
        }

        if (string.IsNullOrWhiteSpace(_options.ContainerName))
        {
            return HealthCheckResult.Unhealthy("AzureBlob:ContainerName is missing.");
        }

        try
        {
            var serviceClient = new BlobServiceClient(_options.ConnectionString);
            var containerClient = serviceClient.GetBlobContainerClient(_options.ContainerName);
            var exists = await containerClient.ExistsAsync(cancellationToken);

            return exists.Value
                ? HealthCheckResult.Healthy("Blob container is reachable.")
                : HealthCheckResult.Unhealthy($"Blob container '{_options.ContainerName}' does not exist.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Blob storage check failed.", ex);
        }
    }
}
