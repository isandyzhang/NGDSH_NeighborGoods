namespace NeighborGoods.Api.Features.Auth.Services;

public interface ILineOAuthClient
{
    string BuildAuthorizeUrl(string state);
    Task<LineOAuthProfile?> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);
}
