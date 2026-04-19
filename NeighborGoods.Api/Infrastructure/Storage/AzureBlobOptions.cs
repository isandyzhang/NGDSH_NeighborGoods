namespace NeighborGoods.Api.Infrastructure.Storage;

public sealed class AzureBlobOptions
{
    public const string SectionName = "AzureBlob";

    public string ConnectionString { get; set; } = string.Empty;

    public string ContainerName { get; set; } = "neighborgoods-images";
}
