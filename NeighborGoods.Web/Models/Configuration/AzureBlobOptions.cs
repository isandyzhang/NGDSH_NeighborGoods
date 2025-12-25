namespace NeighborGoods.Web.Models.Configuration;

public class AzureBlobOptions
{
    public const string SectionName = "AzureBlob";

    /// <summary>
    /// Azure Storage Account 連線字串（必填）。
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Blob Container 名稱（預設：neighborgoods-images）。
    /// </summary>
    public string ContainerName { get; set; } = "neighborgoods-images";
}

