using Microsoft.Extensions.Caching.Memory;

namespace NeighborGoods.Api.Features.Auth.Services;

public sealed class LineOAuthStateStore(IMemoryCache cache) : ILineOAuthStateStore
{
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(5);
    private const string KeyPrefix = "line_oauth_state:";

    public string Create()
    {
        var state = Guid.NewGuid().ToString("N");
        cache.Set(BuildCacheKey(state), "valid", StateTtl);
        return state;
    }

    public bool Consume(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return false;
        }

        var key = BuildCacheKey(state);
        if (!cache.TryGetValue(key, out _))
        {
            return false;
        }

        cache.Remove(key);
        return true;
    }

    private static string BuildCacheKey(string state) => $"{KeyPrefix}{state}";
}
