namespace NeighborGoods.Api.Infrastructure.Storage;

/// <summary>
/// 全站共用 Blob 存取（商品圖、聊天附件等）。路徑命名由各 feature 自行決定。
/// </summary>
public interface IBlobStorage
{
    /// <summary>
    /// 將內容以壓縮 JPEG 上傳至指定 blob 路徑，回傳值與 <paramref name="blobName"/> 相同（供寫入 DB）。
    /// </summary>
    Task<string> UploadCompressedJpegAsync(
        string blobName,
        Stream content,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string blobName, CancellationToken cancellationToken = default);

    string BuildPublicUrl(string blobName);
}
