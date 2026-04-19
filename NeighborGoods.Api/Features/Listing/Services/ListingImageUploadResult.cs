namespace NeighborGoods.Api.Features.Listing.Services;

public sealed record ListingImageUploadResult(
    Guid ImageId,
    int SortOrder,
    string BlobName);
