using NeighborGoods.Api.Features.Listing;

namespace NeighborGoods.Api.Tests;

public sealed class ListingPriceRulesTests
{
    [Theory]
    [InlineData(0, false, 0, true)]
    [InlineData(0, true, 0, true)]
    [InlineData(4000, true, 0, true)]
    [InlineData(4000, false, 4000, false)]
    public void Normalize_UnifiesCreateAndUpdateRules(int price, bool isFree, decimal expectedPrice, bool expectedIsFree)
    {
        var (normalizedPrice, normalizedIsFree) = ListingPriceRules.Normalize(price, isFree);

        Assert.Equal(expectedPrice, normalizedPrice);
        Assert.Equal(expectedIsFree, normalizedIsFree);
    }
}
