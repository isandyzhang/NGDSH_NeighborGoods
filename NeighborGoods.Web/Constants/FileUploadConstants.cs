namespace NeighborGoods.Web.Constants;

/// <summary>
/// 檔案上傳相關常數
/// </summary>
public static class FileUploadConstants
{
    /// <summary>
    /// 最大檔案大小（5MB）
    /// </summary>
    public const long MaxFileSize = 5 * 1024 * 1024; // 5MB

    /// <summary>
    /// 允許的圖片內容類型
    /// </summary>
    public static readonly string[] AllowedContentTypes = 
    { 
        "image/jpeg", 
        "image/jpg", 
        "image/png", 
        "image/gif", 
        "image/webp",
        "image/heic",
        "image/heif",
        "image/heic-sequence"
    };

    /// <summary>
    /// 最大圖片數量
    /// </summary>
    public const int MaxImageCount = 5;
}

