namespace NeighborGoods.Web.Services;

/// <summary>
/// Azure Blob Storage 服務介面，用於處理商品圖片上傳與刪除。
/// </summary>
public interface IBlobService
{
    /// <summary>
    /// 上傳商品圖片到 Blob Storage。
    /// </summary>
    /// <param name="listingId">商品 ID</param>
    /// <param name="imageStream">圖片資料流</param>
    /// <param name="contentType">MIME 類型（例如：image/jpeg）</param>
    /// <param name="sortOrder">排序順序（0-4）</param>
    /// <returns>上傳成功後回傳完整的 Blob URL</returns>
    Task<string> UploadListingImageAsync(Guid listingId, Stream imageStream, string contentType, int sortOrder);

    /// <summary>
    /// 刪除指定的圖片。
    /// </summary>
    /// <param name="blobUrl">要刪除的 Blob URL</param>
    Task DeleteListingImageAsync(string blobUrl);

    /// <summary>
    /// 批次刪除多張圖片。
    /// </summary>
    /// <param name="blobUrls">要刪除的 Blob URL 列表</param>
    Task DeleteListingImagesAsync(List<string> blobUrls);
}

