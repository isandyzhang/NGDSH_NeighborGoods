using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NeighborGoods.Api.Features.Auth.Configuration;

namespace NeighborGoods.Api.Features.Integrations.Line.Services;

public sealed class LineLiffIdTokenVerifier(
    HttpClient httpClient,
    IOptions<LineOAuthOptions> lineOptions) : ILineLiffIdTokenVerifier
{
    private readonly LineOAuthOptions _options = lineOptions.Value;

    public async Task<(string? Sub, string? ErrorCode, string? ErrorMessage)> VerifyAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return (null, "LINE_ID_TOKEN_MISSING", "缺少 LINE id_token。");
        }

        if (string.IsNullOrWhiteSpace(_options.ChannelId))
        {
            return (null, "LINE_OAUTH_MISCONFIGURED", "LINE Login ChannelId 未設定，無法驗證 id_token。");
        }

        var form = new Dictionary<string, string>
        {
            ["id_token"] = idToken.Trim(),
            ["client_id"] = _options.ChannelId,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/oauth2/v2.1/verify")
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var err = TryReadError(body);
            return (null, "LINE_ID_TOKEN_INVALID", err ?? "id_token 驗證失敗。");
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("sub", out var subEl))
        {
            return (null, "LINE_ID_TOKEN_INVALID", "驗證回應缺少 sub。");
        }

        var sub = subEl.GetString();
        return string.IsNullOrWhiteSpace(sub)
            ? (null, "LINE_ID_TOKEN_INVALID", "sub 為空。")
            : (sub, null, null);
    }

    private static string? TryReadError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error_description", out var d))
            {
                return d.GetString();
            }

            if (doc.RootElement.TryGetProperty("error", out var e))
            {
                return e.GetString();
            }
        }
        catch (JsonException)
        {
            // ignore
        }

        return null;
    }
}
