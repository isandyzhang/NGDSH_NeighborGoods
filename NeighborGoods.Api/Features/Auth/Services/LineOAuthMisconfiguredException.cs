namespace NeighborGoods.Api.Features.Auth.Services;

public sealed class LineOAuthMisconfiguredException()
    : InvalidOperationException("LINE OAuth 設定不完整")
{
}
