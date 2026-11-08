namespace NeighborGoods.Api.Features.Listing.Contracts;

public sealed class UpdateListingRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int CategoryCode { get; init; }
    public int ConditionCode { get; init; }
    public int Price { get; init; }
    public int ResidenceCode { get; init; }
    /// <summary>面交地點自由文字（例如：風雨操場、北棟大廳）。</summary>
    public string PickupLocation { get; init; } = string.Empty;

    public bool IsFree { get; init; }
    public bool IsCharity { get; init; }
    public bool IsTradeable { get; init; }

    /// <summary>要刪除的圖片識別：可為 DB 內 raw ImageUrl，或公開 GET 詳情回傳之完整 URL。</summary>
    public IReadOnlyList<string>? ImageUrlsToDelete { get; init; }

    /// <summary>圖片排序：可為 DB 內 raw ImageUrl，或公開 GET 詳情回傳之完整 URL。</summary>
    public IReadOnlyList<string>? ImageUrlsInOrder { get; init; }
}
