using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
namespace NeighborGoods.Api.Tests;

[Collection("ListingApiTests")]
public sealed class ListingEndpointsTests
{
    private const string ConfirmedUserName = "tester";
    private const string UnconfirmedUserName = "novalid";
    private const string UserPassword = "Passw0rd!";

    private readonly SqlServerContainerFixture _fixture;

    public ListingEndpointsTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetListings_ReturnsSuccessEnvelope()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/listings?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.True(body.GetProperty("data").GetProperty("items").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task GetListingById_WhenMissing_ReturnsNotFound()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/listings/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("LISTING_NOT_FOUND", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostListing_CreatesResource()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var request = new
        {
            title = "新刊登測試",
            description = "測試描述",
            categoryCode = 1,
            conditionCode = 2,
            price = 1500,
            residenceCode = 2
        };

        var response = await client.PostAsJsonAsync("/api/v1/listings", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        var id = body.GetProperty("data").GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task PostListing_WhenUnauthenticated_ReturnsUnauthorized()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var request = new
        {
            title = "匿名上架",
            description = "不應成功",
            categoryCode = 1,
            conditionCode = 2,
            price = 100,
            residenceCode = 2
        };

        var response = await client.PostAsJsonAsync("/api/v1/listings", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostListing_WhenEmailNotConfirmed_ReturnsForbidden()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, UnconfirmedUserName, UserPassword);

        var request = new
        {
            title = "未驗證上架",
            description = "不應成功",
            categoryCode = 1,
            conditionCode = 2,
            price = 100,
            residenceCode = 2
        };

        var response = await client.PostAsJsonAsync("/api/v1/listings", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("EMAIL_NOT_CONFIRMED", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PutListing_WhenMissing_ReturnsNotFound()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var request = new
        {
            title = "更新測試",
            description = "更新內容",
            categoryCode = 3,
            conditionCode = 1,
            price = 900,
            residenceCode = 1
        };

        var response = await client.PutAsJsonAsync($"/api/v1/listings/{Guid.NewGuid()}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("LISTING_NOT_FOUND", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeleteListing_DeletesResource()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var createRequest = new
        {
            title = "待刪除商品",
            description = "測試刪除",
            categoryCode = 5,
            conditionCode = 2,
            price = 500,
            residenceCode = 3
        };

        var createResponse = await client.PostAsJsonAsync("/api/v1/listings", createRequest);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = createBody.GetProperty("data").GetProperty("id").GetGuid();

        var deleteResponse = await client.DeleteAsync($"/api/v1/listings/{id}");

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var deleteBody = await deleteResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(deleteBody.GetProperty("success").GetBoolean());
        Assert.True(deleteBody.GetProperty("data").GetProperty("deleted").GetBoolean());
    }

    [Fact]
    public async Task PatchReserveListing_FromActive_ReturnsOk()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "狀態測試-ActiveToReserved");

        var response = await client.PatchAsync($"/api/v1/listings/{id}/reserve", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());

        var getResponse = await client.GetAsync($"/api/v1/listings/{id}");
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, getBody.GetProperty("data").GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task PatchReserveListing_WhenMissing_ReturnsNotFound()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var response = await client.PatchAsync($"/api/v1/listings/{Guid.NewGuid()}/reserve", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("LISTING_NOT_FOUND", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PatchReserveListing_FromSold_ReturnsConflict()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "狀態測試-SoldToReserved");
        var soldResponse = await client.PatchAsync($"/api/v1/listings/{id}/sold", content: null);
        Assert.Equal(HttpStatusCode.OK, soldResponse.StatusCode);

        var response = await client.PatchAsync($"/api/v1/listings/{id}/reserve", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("LISTING_INVALID_STATUS_TRANSITION", body.GetProperty("error").GetProperty("code").GetString());
    }

    private static async Task<Guid> CreateListingAsync(HttpClient client, string title)
    {
        var createRequest = new
        {
            title,
            description = "狀態測試建立資料",
            categoryCode = 1,
            conditionCode = 1,
            price = 1000,
            residenceCode = 2
        };

        var createResponse = await client.PostAsJsonAsync("/api/v1/listings", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        return createBody.GetProperty("data").GetProperty("id").GetGuid();
    }

    private static async Task AuthenticateAsAsync(HttpClient client, string userNameOrEmail, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userNameOrEmail,
            password
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = body.GetProperty("data").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }
}
