namespace NeighborGoods.Api.Features.Listing.Contracts;

public sealed class ListingQueryRequest
{
    public string? Query { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    /// <summary>僅在為 true 時套用：只顯示免費商品（與其他 true 篩選為 AND）。</summary>
    public bool? IsFree { get; init; }

    /// <summary>僅在為 true 時套用：只顯示愛心商品（與其他 true 篩選為 AND）。</summary>
    public bool? IsCharity { get; init; }

    /// <summary>僅在為 true 時套用：只顯示可易物商品（與其他 true 篩選為 AND）。</summary>
    public bool? IsTradeable { get; init; }

    public int? CategoryCode { get; init; }
    public int? ConditionCode { get; init; }
    public int? ResidenceCode { get; init; }
    public IReadOnlyCollection<int>? CategoryCodes { get; init; }
    public IReadOnlyCollection<int>? ConditionCodes { get; init; }
    public IReadOnlyCollection<int>? ResidenceCodes { get; init; }
    public int? MinPrice { get; init; }
    public int? MaxPrice { get; init; }

    /// <summary>排除該賣家 userId 的商品（與 Web Search 的 ExcludeUserId 一致）。</summary>
    public string? ExcludeUserId { get; init; }
}
