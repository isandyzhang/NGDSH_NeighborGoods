using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NeighborGoods.Api.Features.Auth.Configuration;

namespace NeighborGoods.Api.Features.Auth.Services;

public sealed class LineOAuthClient(
    HttpClient httpClient,
    IOptions<LineOAuthOptions> lineOptions) : ILineOAuthClient
{
    private readonly LineOAuthOptions _options = lineOptions.Value;

    public string BuildAuthorizeUrl(string state)
    {
        EnsureLineOptions();

        var scope = Uri.EscapeDataString(_options.Scope);
        var callback = Uri.EscapeDataString(_options.CallbackUrl);
        var encodedState = Uri.EscapeDataString(state);

        return $"https://access.line.me/oauth2/v2.1/authorize?response_type=code&client_id={_options.ChannelId}&redirect_uri={callback}&state={encodedState}&scope={scope}";
    }

    public async Task<LineOAuthProfile?> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        EnsureLineOptions();

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _options.CallbackUrl,
            ["client_id"] = _options.ChannelId,
            ["client_secret"] = _options.ChannelSecret
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/oauth2/v2.1/token")
        {
            Content = new FormUrlEncodedContent(form)
        };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<LineTokenResponse>(contentStream, cancellationToken: cancellationToken);
        if (payload is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(payload.IdToken))
        {
            var principal = ValidateIdToken(payload.IdToken);
            if (principal is not null)
            {
                var subject = principal.FindFirst("sub")?.Value;
                if (!string.IsNullOrWhiteSpace(subject))
                {
                    var displayNameFromIdToken = principal.FindFirst("name")?.Value;
                    if (string.IsNullOrWhiteSpace(displayNameFromIdToken))
                    {
                        displayNameFromIdToken = "LINE 使用者";
                    }

                    return new LineOAuthProfile(subject, displayNameFromIdToken);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            return null;
        }

        return await FetchProfileByUserInfoAsync(payload.AccessToken, cancellationToken);
    }

    private async Task<LineOAuthProfile?> FetchProfileByUserInfoAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.line.me/oauth2/v2.1/userinfo");
        request.Headers.Authorization = new global::System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var profile = await JsonSerializer.DeserializeAsync<LineUserInfoResponse>(contentStream, cancellationToken: cancellationToken);
        if (profile is null || string.IsNullOrWhiteSpace(profile.Subject))
        {
            return null;
        }

        var displayName = string.IsNullOrWhiteSpace(profile.Name) ? "LINE 使用者" : profile.Name;
        return new LineOAuthProfile(profile.Subject, displayName);
    }

    private ClaimsPrincipal? ValidateIdToken(string idToken)
    {
        var validation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://access.line.me",
            ValidateAudience = true,
            ValidAudience = _options.ChannelId,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ChannelSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        var handler = new JwtSecurityTokenHandler();
        try
        {
            return handler.ValidateToken(idToken, validation, out _);
        }
        catch
        {
            return null;
        }
    }

    private void EnsureLineOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.ChannelId) ||
            string.IsNullOrWhiteSpace(_options.ChannelSecret) ||
            string.IsNullOrWhiteSpace(_options.CallbackUrl))
        {
            throw new InvalidOperationException("Line OAuth settings are incomplete.");
        }
    }

    private sealed class LineTokenResponse
    {
        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }

        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }
    }

    private sealed class LineUserInfoResponse
    {
        [JsonPropertyName("sub")]
        public string? Subject { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }
}
