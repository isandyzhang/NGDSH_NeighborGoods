using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace NeighborGoods.Api.Tests;

[Collection("ListingApiTests")]
public sealed class MessagingEndpointsTests
{
    private static readonly JsonSerializerOptions CamelCaseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Guid SeededListingId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string SellerUserId = "test-user-confirmed";
    private const string BuyerUserId = "test-user-other";
    private const string ThirdUserId = "test-user-unconfirmed";
    private const string UserPassword = "Passw0rd!";

    private readonly SqlServerContainerFixture _fixture;

    public MessagingEndpointsTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ListConversations_WithoutAuth_Returns401()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/conversations");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EnsureConversation_BuyerWithSeller_ReturnsConversationId()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "other@example.com", UserPassword);

        var response = await client.PostAsJsonAsync(
            "/api/v1/conversations",
            new { listingId = SeededListingId, otherUserId = SellerUserId },
            CamelCaseJson);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        var convId = body.GetProperty("data").GetProperty("conversationId").GetGuid();
        Assert.NotEqual(Guid.Empty, convId);
    }

    [Fact]
    public async Task EnsureConversation_SameParticipantsFromSeller_ReturnsSameConversationId()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        await AuthenticateAsAsync(client, "other@example.com", UserPassword);
        var buyerResponse = await client.PostAsJsonAsync(
            "/api/v1/conversations",
            new { listingId = SeededListingId, otherUserId = SellerUserId },
            CamelCaseJson);
        buyerResponse.EnsureSuccessStatusCode();
        var buyerBody = await buyerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var idFromBuyer = buyerBody.GetProperty("data").GetProperty("conversationId").GetGuid();

        client.DefaultRequestHeaders.Authorization = null;
        await AuthenticateAsAsync(client, "tester@example.com", UserPassword);
        var sellerResponse = await client.PostAsJsonAsync(
            "/api/v1/conversations",
            new { listingId = SeededListingId, otherUserId = BuyerUserId },
            CamelCaseJson);
        sellerResponse.EnsureSuccessStatusCode();
        var sellerBody = await sellerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var idFromSeller = sellerBody.GetProperty("data").GetProperty("conversationId").GetGuid();

        Assert.Equal(idFromBuyer, idFromSeller);
    }

    [Fact]
    public async Task EnsureConversation_NeitherUserIsSeller_Returns400()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "other@example.com", UserPassword);

        var response = await client.PostAsJsonAsync(
            "/api/v1/conversations",
            new { listingId = SeededListingId, otherUserId = ThirdUserId },
            CamelCaseJson);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal(
            "INVALID_CONVERSATION_PARTICIPANTS",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task SendMessage_AndList_MarkRead_UpdatesUnread()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "other@example.com", UserPassword);

        var ensure = await client.PostAsJsonAsync(
            "/api/v1/conversations",
            new { listingId = SeededListingId, otherUserId = SellerUserId },
            CamelCaseJson);
        ensure.EnsureSuccessStatusCode();
        var convId = (await ensure.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("conversationId").GetGuid();

        var send = await client.PostAsJsonAsync(
            $"/api/v1/conversations/{convId}/messages",
            new { content = "測試訊息" },
            CamelCaseJson);
        send.EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Authorization = null;
        await AuthenticateAsAsync(client, "tester@example.com", UserPassword);

        var list = await client.GetAsync("/api/v1/conversations");
        list.EnsureSuccessStatusCode();
        var listBody = await list.Content.ReadFromJsonAsync<JsonElement>();
        var items = listBody.GetProperty("data").GetProperty("items");
        var foundUnread = false;
        foreach (var el in items.EnumerateArray())
        {
            if (el.GetProperty("conversationId").GetGuid() != convId)
            {
                continue;
            }

            foundUnread = true;
            Assert.True(el.GetProperty("unreadCount").GetInt32() >= 1);
            break;
        }

        Assert.True(foundUnread);

        var read = await client.PostAsync($"/api/v1/conversations/{convId}/read", null);
        read.EnsureSuccessStatusCode();

        var list2 = await client.GetAsync("/api/v1/conversations");
        list2.EnsureSuccessStatusCode();
        var listBody2 = await list2.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var el in listBody2.GetProperty("data").GetProperty("items").EnumerateArray())
        {
            if (el.GetProperty("conversationId").GetGuid() == convId)
            {
                Assert.Equal(0, el.GetProperty("unreadCount").GetInt32());
                break;
            }
        }
    }

    [Fact]
    public async Task GetMessages_NonParticipant_Returns403()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "other@example.com", UserPassword);

        var ensure = await client.PostAsJsonAsync(
            "/api/v1/conversations",
            new { listingId = SeededListingId, otherUserId = SellerUserId },
            CamelCaseJson);
        ensure.EnsureSuccessStatusCode();
        var convId = (await ensure.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("conversationId").GetGuid();

        client.DefaultRequestHeaders.Authorization = null;
        await AuthenticateAsAsync(client, "novalid@example.com", UserPassword);

        var response = await client.GetAsync($"/api/v1/conversations/{convId}/messages");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMessages_ReturnsPagedMessages()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "other@example.com", UserPassword);

        var ensure = await client.PostAsJsonAsync(
            "/api/v1/conversations",
            new { listingId = SeededListingId, otherUserId = SellerUserId },
            CamelCaseJson);
        var convId = (await ensure.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("conversationId").GetGuid();

        await client.PostAsJsonAsync(
            $"/api/v1/conversations/{convId}/messages",
            new { content = "A" },
            CamelCaseJson);

        var page = await client.GetAsync($"/api/v1/conversations/{convId}/messages?page=1&pageSize=10");
        page.EnsureSuccessStatusCode();
        var body = await page.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("data").GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);
        Assert.Equal("A", items[items.GetArrayLength() - 1].GetProperty("content").GetString());
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
