using NeighborGoods.Api.Features.Integrations.Line.Services;

namespace NeighborGoods.Api.Tests;

internal sealed class FakeLineLiffIdTokenVerifier : ILineLiffIdTokenVerifier
{
    public const string ValidTestIdToken = "test-liff-id-token-valid";
    public const string ValidTestSub = "line-user-liff-test-sub";

    public Task<(string? Sub, string? ErrorCode, string? ErrorMessage)> VerifyAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(idToken.Trim(), ValidTestIdToken, StringComparison.Ordinal))
        {
            return Task.FromResult<(string?, string?, string?)>((ValidTestSub, null, null));
        }

        return Task.FromResult<(string?, string?, string?)>((null, "LINE_ID_TOKEN_INVALID", "測試用 id_token 無效。"));
    }
}
