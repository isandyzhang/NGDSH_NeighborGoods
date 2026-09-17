namespace NeighborGoods.Api.Features.Admin;

public sealed class AdminHousingException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
