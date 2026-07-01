using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NeighborGoods.Api.Features.Integrations.Ado;

namespace NeighborGoods.Api.Tests;

[Collection("ListingApiTests")]
public sealed class AdoWebhookEndpointsTests(SqlServerContainerFixture fixture)
{
    private const string UserPassword = "Passw0rd!";

    [Fact]
    public async Task PostWebhook_StoresRawBody_AndReturnsOk()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var payload = "{\"eventType\":\"workitem.updated\",\"resource\":{\"id\":\"123\"}}";

        var response = await client.PostAsync(
            "/api/v1/integrations/ado/webhook",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<AdoWebhookMemoryStore>();
        var (items, totalCount) = store.List(1, 10);
        Assert.Equal(1, totalCount);
        Assert.Equal(payload, items[0].RawBody);
    }

    [Fact]
    public async Task GetAdminWebhookEvents_AsAdmin_ReturnsStoredEvents()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var payload = "{\"eventType\":\"build.complete\"}";

        var postResponse = await client.PostAsync(
            "/api/v1/integrations/ado/webhook",
            new StringContent(payload, Encoding.UTF8, "application/json"));
        postResponse.EnsureSuccessStatusCode();

        await AuthenticateAsAsync(client, ListingApiFactory.AdminUserName, UserPassword);
        var response = await client.GetAsync("/api/v1/admin/ado-webhook-events");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        var items = body.GetProperty("data").GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);
        Assert.Contains(
            "build.complete",
            items.EnumerateArray().Select(x => x.GetProperty("rawBodyPreview").GetString()));
    }

    [Fact]
    public async Task GetAdminWebhookEvents_AsNonAdmin_ReturnsForbidden()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "tester", UserPassword);

        var response = await client.GetAsync("/api/v1/admin/ado-webhook-events");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAdminWebhookEventDetail_AsAdmin_ReturnsFullRawBody()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var payload = "{\"eventType\":\"workitem.created\",\"resource\":{\"id\":\"999\"}}";

        var postResponse = await client.PostAsync(
            "/api/v1/integrations/ado/webhook",
            new StringContent(payload, Encoding.UTF8, "application/json"));
        postResponse.EnsureSuccessStatusCode();

        Guid eventId;
        using (var scope = factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<AdoWebhookMemoryStore>();
            eventId = store.List(1, 1).Items[0].Id;
        }

        await AuthenticateAsAsync(client, ListingApiFactory.AdminUserName, UserPassword);
        var response = await client.GetAsync($"/api/v1/admin/ado-webhook-events/{eventId}");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(payload, body.GetProperty("data").GetProperty("rawBody").GetString());
    }

    private static async Task AuthenticateAsAsync(HttpClient client, string userNameOrEmail, string password)
    {
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { userNameOrEmail, password });
        loginResponse.EnsureSuccessStatusCode();
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("data").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
