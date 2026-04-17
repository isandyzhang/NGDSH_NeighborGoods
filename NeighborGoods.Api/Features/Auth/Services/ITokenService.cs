using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Features.Auth.Services;

public interface ITokenService
{
    Task<AuthTokenPair> IssueAsync(AspNetUser user, CancellationToken cancellationToken = default);
    Task<AuthTokenPair?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);
}
