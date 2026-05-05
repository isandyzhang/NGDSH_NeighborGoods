namespace NeighborGoods.Api.Features.Integrations.Line.Services;

public interface ILineLiffIdTokenVerifier
{
    /// <summary>
    /// 呼叫 LINE oauth2/v2.1/verify；成功時回傳 JWT 的 sub（LINE user id）。
    /// </summary>
    Task<(string? Sub, string? ErrorCode, string? ErrorMessage)> VerifyAsync(
        string idToken,
        CancellationToken cancellationToken = default);
}
