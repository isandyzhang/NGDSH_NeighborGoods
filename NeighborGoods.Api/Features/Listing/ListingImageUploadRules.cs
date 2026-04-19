namespace NeighborGoods.Api.Features.Listing;

public static class ListingImageUploadRules
{
    public const long MaxFileSize = 5 * 1024 * 1024;

    public const int MaxImageCount = 5;

    public static readonly HashSet<string> AllowedContentTypes =
    [
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/heic",
        "image/heif",
        "image/heic-sequence"
    ];
}
