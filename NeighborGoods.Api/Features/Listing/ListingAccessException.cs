namespace NeighborGoods.Api.Features.Listing;

public sealed class ListingAccessException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
