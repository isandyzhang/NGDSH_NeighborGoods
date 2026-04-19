namespace NeighborGoods.Api.Features.Listing.Contracts;

public sealed class CreateListingRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int CategoryCode { get; init; }
    public int ConditionCode { get; init; }
    public int Price { get; init; }
    public int ResidenceCode { get; init; }
    public int PickupLocationCode { get; init; }

    /// <summary>與 Web 刊登表單一致；未帶 JSON 時視為 false。</summary>
    public bool IsFree { get; init; }

    public bool IsCharity { get; init; }
    public bool IsTradeable { get; init; }

    /// <summary>建立時是否扣 1 點置頂次數並開啟 7 天置頂（與 Web UseTopPin）。</summary>
    public bool UseTopPin { get; init; }
}
