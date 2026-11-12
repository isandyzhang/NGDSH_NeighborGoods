using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeighborGoods.Data;
using NeighborGoods.Data.Line;
using NeighborGoods.Notifications;

namespace NeighborGoods.Api.Tests;

[Collection("ListingApiTests")]
public sealed class LineTransactionGroupTests
{
    private const string ChannelSecret = "line-msg-test-secret";
    private const string ConfirmedUserName = "tester";
    private const string OtherUserName = ListingApiFactory.OtherConfirmedUserName;
    private const string AdminUserName = ListingApiFactory.AdminUserName;
    private const string UserPassword = "Passw0rd!";
    private static readonly Guid SeededTesterListingId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly SqlServerContainerFixture _fixture;

    public LineTransactionGroupTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Webhook_InvalidSignature_ReturnsUnauthorized()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/line/webhook")
        {
            Content = new StringContent("""{"events":[]}""", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Line-Signature", "invalid");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_Join_RegistersActiveGroup_SecondDifferentGroup_DoesNotOverride()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var joinBody = BuildGroupEventBody("join", "C-group-active-1");
        var joinResponse = await PostWebhookAsync(client, joinBody);
        Assert.Equal(HttpStatusCode.OK, joinResponse.StatusCode);

        var secondBody = BuildGroupEventBody("join", "C-group-other-2");
        var secondResponse = await PostWebhookAsync(client, secondBody);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var groups = await db.LineTransactionGroups.AsNoTracking().ToListAsync();
        Assert.Equal(2, groups.Count);
        Assert.Single(groups, g => g.IsActive && g.GroupId == "C-group-active-1");
        Assert.Contains(groups, g => !g.IsActive && g.GroupId == "C-group-other-2");
    }

    [Fact]
    public async Task Webhook_Leave_DeactivatesActiveGroup()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        await PostWebhookAsync(client, BuildGroupEventBody("join", "C-group-leave-1"));
        var leaveResponse = await PostWebhookAsync(client, BuildGroupEventBody("leave", "C-group-leave-1"));
        Assert.Equal(HttpStatusCode.OK, leaveResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var group = await db.LineTransactionGroups.AsNoTracking()
            .SingleAsync(x => x.GroupId == "C-group-leave-1");
        Assert.False(group.IsActive);
        Assert.NotNull(group.LeftAt);
    }

    [Fact]
    public async Task ShareToTransactionGroup_NonSeller_Forbidden()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await SeedActiveGroupAsync(factory, "C-share-forbid");
        await AuthenticateAsAsync(client, OtherUserName, UserPassword);

        var response = await client.PostAsync(
            $"/api/v1/listings/{SeededTesterListingId}/share-to-transaction-group",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(FakeLineMessageSender.PushFlexMessages);
    }

    [Fact]
    public async Task ShareToTransactionGroup_WithoutGroup_DoesNotPush()
    {
        FakeLineMessageSender.Reset();
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var response = await client.PostAsync(
            $"/api/v1/listings/{SeededTesterListingId}/share-to-transaction-group",
            null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LINE_TRANSACTION_GROUP_MISSING", body.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(FakeLineMessageSender.PushFlexMessages);
    }

    [Fact]
    public async Task ShareToTransactionGroup_QuotaInsufficient_DoesNotPush()
    {
        FakeLineMessageSender.Reset();
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await SeedActiveGroupAsync(factory, "C-share-quota");

        var quota = factory.Services.GetRequiredService<ILineMessagingQuotaService>() as FakeLineMessagingQuotaService;
        Assert.NotNull(quota);
        quota!.MemberCount = 50;
        quota.RemainingCount = 10;

        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);
        var response = await client.PostAsync(
            $"/api/v1/listings/{SeededTesterListingId}/share-to-transaction-group",
            null);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Empty(FakeLineMessageSender.PushFlexMessages);
    }

    [Fact]
    public async Task ShareToTransactionGroup_Seller_PushesToGroup()
    {
        FakeLineMessageSender.Reset();
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await SeedActiveGroupAsync(factory, "C-share-ok");

        var quota = factory.Services.GetRequiredService<ILineMessagingQuotaService>() as FakeLineMessagingQuotaService;
        Assert.NotNull(quota);
        quota!.MemberCount = 4;
        quota.RemainingCount = 100;

        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);
        var response = await client.PostAsync(
            $"/api/v1/listings/{SeededTesterListingId}/share-to-transaction-group",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(FakeLineMessageSender.PushFlexMessages);
        Assert.Equal("C-share-ok", FakeLineMessageSender.PushFlexMessages[0].UserId);
    }

    [Fact]
    public async Task Admin_GetAndActivateTransactionGroup()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await SeedActiveGroupAsync(factory, "C-admin-active");
        Guid inactiveId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var inactive = new LineTransactionGroup
            {
                Id = Guid.NewGuid(),
                GroupId = "C-admin-inactive",
                IsActive = false,
                JoinedAt = DateTime.UtcNow.AddMinutes(-5)
            };
            db.LineTransactionGroups.Add(inactive);
            await db.SaveChangesAsync();
            inactiveId = inactive.Id;
        }

        await AuthenticateAsAsync(client, AdminUserName, UserPassword);
        var getResponse = await client.GetAsync("/api/v1/admin/line/transaction-group");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("C-admin-active", getBody.GetProperty("data").GetProperty("active").GetProperty("groupId").GetString());

        var activateResponse = await client.PostAsync(
            $"/api/v1/admin/line/transaction-group/{inactiveId}/activate",
            null);
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        var activateBody = await activateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            "C-admin-inactive",
            activateBody.GetProperty("data").GetProperty("active").GetProperty("groupId").GetString());
    }

    private static async Task SeedActiveGroupAsync(ListingApiFactory factory, string groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        db.LineTransactionGroups.Add(new LineTransactionGroup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> PostWebhookAsync(HttpClient client, string body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/line/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Line-Signature", Sign(body));
        return await client.SendAsync(request);
    }

    private static string BuildGroupEventBody(string eventType, string groupId) =>
        $$"""
        {
          "events": [
            {
              "type": "{{eventType}}",
              "source": {
                "type": "group",
                "groupId": "{{groupId}}",
                "userId": "U-tester"
              }
            }
          ]
        }
        """;

    private static string Sign(string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(ChannelSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToBase64String(hash);
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
