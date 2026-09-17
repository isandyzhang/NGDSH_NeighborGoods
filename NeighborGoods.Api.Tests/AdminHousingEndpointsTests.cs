using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace NeighborGoods.Api.Tests;

[Collection("ListingApiTests")]
public sealed class AdminHousingEndpointsTests(SqlServerContainerFixture fixture)
{
    private const string UserPassword = "Passw0rd!";
    private const string ConfirmedUserName = "tester";

    [Fact]
    public async Task AdminResidences_NonAdmin_ReturnsForbidden()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var response = await client.GetAsync("/api/v1/admin/residences");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminHousing_CreateResidenceAndPickup_ThenPublicLookupFilters()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ListingApiFactory.AdminUserName, UserPassword);

        var createResidence = await client.PostAsJsonAsync(
            "/api/v1/admin/residences",
            new
            {
                displayName = "測試社宅",
                city = "臺北市",
                district = "南港區",
                notes = "後台測試",
                sortOrder = 10,
                isActive = true,
            });
        createResidence.EnsureSuccessStatusCode();
        var residence = await createResidence.Content.ReadFromJsonAsync<JsonElement>();
        var residenceId = residence.GetProperty("data").GetProperty("id").GetInt32();
        Assert.Equal("臺北市", residence.GetProperty("data").GetProperty("city").GetString());

        var createPickup = await client.PostAsJsonAsync(
            "/api/v1/admin/pickup-locations",
            new
            {
                displayName = "大門警衛室",
                residenceId,
                sortOrder = 0,
                isActive = true,
            });
        createPickup.EnsureSuccessStatusCode();
        var pickup = await createPickup.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(residenceId, pickup.GetProperty("data").GetProperty("residenceId").GetInt32());

        var lookupResponse = await client.GetAsync($"/api/v1/lookups/pickup-locations?residenceId={residenceId}");
        lookupResponse.EnsureSuccessStatusCode();
        var lookup = await lookupResponse.Content.ReadFromJsonAsync<JsonElement>();
        var names = lookup.GetProperty("data").EnumerateArray()
            .Select(x => x.GetProperty("displayName").GetString())
            .ToHashSet();
        Assert.Contains("大門警衛室", names);
        Assert.Contains("私訊", names);
        Assert.DoesNotContain("北棟管理室", names);
    }

    [Fact]
    public async Task AdminResidences_DeleteInUse_ReturnsConflict()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ListingApiFactory.AdminUserName, UserPassword);

        var response = await client.DeleteAsync("/api/v1/admin/residences/2");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LOOKUP_IN_USE", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task AdminResidences_DeleteProtected_ReturnsConflict()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ListingApiFactory.AdminUserName, UserPassword);

        var response = await client.DeleteAsync("/api/v1/admin/residences/0");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LOOKUP_PROTECTED", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task AdminPickupLocations_DeleteMessage_ReturnsConflict()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ListingApiFactory.AdminUserName, UserPassword);

        var response = await client.DeleteAsync("/api/v1/admin/pickup-locations/3");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LOOKUP_PROTECTED", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task AdminResidences_DeleteUnused_Succeeds()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ListingApiFactory.AdminUserName, UserPassword);

        var createResidence = await client.PostAsJsonAsync(
            "/api/v1/admin/residences",
            new
            {
                displayName = "可刪社宅",
                city = (string?)null,
                district = (string?)null,
                notes = (string?)null,
                sortOrder = 20,
                isActive = true,
            });
        createResidence.EnsureSuccessStatusCode();
        var residenceId = (await createResidence.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("id").GetInt32();

        var deleteResponse = await client.DeleteAsync($"/api/v1/admin/residences/{residenceId}");
        deleteResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AdminCategories_CreateAndDisable_HidesFromPublicLookup()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ListingApiFactory.AdminUserName, UserPassword);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/categories",
            new { displayName = "測試分類", sortOrder = 99, isActive = true });
        createResponse.EnsureSuccessStatusCode();
        var id = (await createResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("id").GetInt32();

        var enabledLookup = await client.GetAsync("/api/v1/lookups/categories");
        enabledLookup.EnsureSuccessStatusCode();
        var enabledNames = (await enabledLookup.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").EnumerateArray()
            .Select(x => x.GetProperty("displayName").GetString())
            .ToHashSet();
        Assert.Contains("測試分類", enabledNames);

        var disableResponse = await client.PatchAsJsonAsync(
            $"/api/v1/admin/categories/{id}/enabled",
            new { isEnabled = false });
        disableResponse.EnsureSuccessStatusCode();

        var disabledLookup = await client.GetAsync("/api/v1/lookups/categories");
        disabledLookup.EnsureSuccessStatusCode();
        var disabledNames = (await disabledLookup.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").EnumerateArray()
            .Select(x => x.GetProperty("displayName").GetString())
            .ToHashSet();
        Assert.DoesNotContain("測試分類", disabledNames);
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
