using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace NeighborGoods.Api.Tests;

[Collection("ListingApiTests")]
public sealed class AuthEndpointsTests
{
    private const string ConfirmedUserName = "tester";
    private const string UserPassword = "Passw0rd!";

    private readonly SqlServerContainerFixture _fixture;

    public AuthEndpointsTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Login_WithValidPassword_ReturnsTokenPair()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userNameOrEmail = ConfirmedUserName,
            password = UserPassword
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("data").GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("data").GetProperty("refreshToken").GetString()));
    }

    [Fact]
    public async Task Refresh_AfterRevoke_ReturnsUnauthorized()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userNameOrEmail = ConfirmedUserName,
            password = UserPassword
        });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = loginBody.GetProperty("data").GetProperty("refreshToken").GetString();

        var revokeResponse = await client.PostAsJsonAsync("/api/v1/auth/revoke", new
        {
            refreshToken
        });
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);

        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken
        });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithValidToken_RotatesToken()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userNameOrEmail = ConfirmedUserName,
            password = UserPassword
        });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = loginBody.GetProperty("data").GetProperty("refreshToken").GetString();

        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken
        });
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>();
        var rotatedRefreshToken = refreshBody.GetProperty("data").GetProperty("refreshToken").GetString();
        Assert.NotEqual(refreshToken, rotatedRefreshToken);

        var oldRefreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefreshResponse.StatusCode);
    }

    [Fact]
    public async Task LineLoginCallback_WhenCodeValid_ReturnsTokens()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var loginRedirectResponse = await client.GetAsync("/api/v1/auth/line/login");
        Assert.Equal(HttpStatusCode.Redirect, loginRedirectResponse.StatusCode);
        var location = loginRedirectResponse.Headers.Location;
        Assert.NotNull(location);
        var state = ExtractQueryParameter(location!, "state");
        Assert.False(string.IsNullOrWhiteSpace(state));

        var callbackResponse = await client.GetAsync($"/api/v1/auth/line/callback?code=line-ok&state={Uri.EscapeDataString(state!)}");
        Assert.Equal(HttpStatusCode.OK, callbackResponse.StatusCode);
        var body = await callbackResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("data").GetProperty("accessToken").GetString()));
    }

    private static string? ExtractQueryParameter(Uri url, string key)
    {
        var query = url.Query.TrimStart('?');
        var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0] == key)
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return null;
    }
}
