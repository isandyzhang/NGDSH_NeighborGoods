using Microsoft.AspNetCore.DataProtection;

namespace NeighborGoods.Api.Features.Auth.Services;

public sealed class LineOAuthStateStore(IDataProtectionProvider dataProtectionProvider) : ILineOAuthStateStore
{
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(5);
    private readonly ITimeLimitedDataProtector _protector = dataProtectionProvider
        .CreateProtector("NeighborGoods.Auth.LineOAuthState.v1")
        .ToTimeLimitedDataProtector();

    public string Create()
    {
        var nonce = Guid.NewGuid().ToString("N");
        return _protector.Protect(nonce, StateTtl);
    }

    public bool Consume(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return false;
        }

        try
        {
            var nonce = _protector.Unprotect(state);
            return !string.IsNullOrWhiteSpace(nonce);
        }
        catch
        {
            return false;
        }
    }
}
