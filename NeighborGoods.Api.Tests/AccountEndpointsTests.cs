using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeighborGoods.Api.Shared.Persistence;

namespace NeighborGoods.Api.Tests;

[Collection("ListingApiTests")]
public sealed class AccountEndpointsTests(SqlServerContainerFixture fixture)
{
    private const string UserPassword = "Passw0rd!";

    [Fact]
    public async Task RegisterSendCode_ReturnsSuccess()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/account/register/send-code",
            new { email = "newuser@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(FakeEmailSender.GetCode("newuser@example.com"));
    }

    [Fact]
    public async Task Register_WithValidCode_CreatesUserAndReturnsTokens()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var email = "register-ok@example.com";

        var sendCodeResponse = await client.PostAsJsonAsync(
            "/api/v1/account/register/send-code",
            new { email });
        sendCodeResponse.EnsureSuccessStatusCode();
        var code = FakeEmailSender.GetCode(email);
        Assert.False(string.IsNullOrWhiteSpace(code));

        var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/account/register",
            new
            {
                userName = "register_ok_user",
                displayName = "Register Ok",
                email,
                password = "RegisterPass!123",
                emailVerificationCode = code
            });

        registerResponse.EnsureSuccessStatusCode();
        var body = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("data").GetProperty("accessToken").GetString()));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var user = await db.AspNetUsers.FirstOrDefaultAsync(x => x.NormalizedEmail == email.ToUpperInvariant());
        Assert.NotNull(user);
        Assert.True(user!.EmailConfirmed);
    }

    [Fact]
    public async Task Register_WithInvalidCode_ReturnsBadRequest()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var sendCodeResponse = await client.PostAsJsonAsync(
            "/api/v1/account/register/send-code",
            new { email = "register-fail@example.com" });
        sendCodeResponse.EnsureSuccessStatusCode();

        var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/account/register",
            new
            {
                userName = "register_fail_user",
                displayName = "Register Fail",
                email = "register-fail@example.com",
                password = "RegisterPass!123",
                emailVerificationCode = "000000"
            });

        Assert.Equal(HttpStatusCode.BadRequest, registerResponse.StatusCode);
        var body = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("EMAIL_CODE_INVALID", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ListingEmailSendCode_WithoutAuth_ReturnsUnauthorized()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/account/email/send-code",
            new { email = "who@example.com" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListingEmailVerify_WithValidCode_UpdatesUserEmailFlags()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "novalid@example.com", UserPassword);

        const string targetEmail = "listing-verified@example.com";
        var sendResponse = await client.PostAsJsonAsync(
            "/api/v1/account/email/send-code",
            new { email = targetEmail });
        sendResponse.EnsureSuccessStatusCode();

        var code = FakeEmailSender.GetCode(targetEmail);
        Assert.False(string.IsNullOrWhiteSpace(code));

        var verifyResponse = await client.PostAsJsonAsync(
            "/api/v1/account/email/verify",
            new
            {
                email = targetEmail,
                code
            });
        verifyResponse.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var user = await db.AspNetUsers.FirstAsync(x => x.NormalizedUserName == "NOVALID");
        Assert.Equal(targetEmail, user.Email);
        Assert.True(user.EmailConfirmed);
        Assert.True(user.EmailNotificationEnabled);
    }

    [Fact]
    public async Task GetMe_ReturnsCurrentUserData()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "tester@example.com", UserPassword);

        var response = await client.GetAsync("/api/v1/account/me");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("tester", body.GetProperty("data").GetProperty("displayName").GetString());
        Assert.True(body.GetProperty("data").GetProperty("statistics").GetProperty("totalListings").GetInt32() >= 1);
    }

    [Fact]
    public async Task PatchMe_UpdatesDisplayName()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "tester@example.com", UserPassword);

        var patchResponse = await client.PatchAsJsonAsync(
            "/api/v1/account/me",
            new { displayName = "Tester Updated" });
        patchResponse.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var user = await db.AspNetUsers.FirstAsync(x => x.NormalizedUserName == "TESTER");
        Assert.Equal("Tester Updated", user.DisplayName);
    }

    [Fact]
    public async Task LineBinding_FollowConfirmAndUnbind_WorksEndToEnd()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "other@example.com", UserPassword);

        var startResponse = await client.PostAsync("/api/v1/account/line/bind/start", null);
        startResponse.EnsureSuccessStatusCode();
        var startBody = await startResponse.Content.ReadFromJsonAsync<JsonElement>();
        var pendingId = startBody.GetProperty("data").GetProperty("pendingBindingId").GetGuid();

        var waitingResponse = await client.GetAsync($"/api/v1/account/line/bind/status?pendingBindingId={pendingId}");
        waitingResponse.EnsureSuccessStatusCode();
        var waitingBody = await waitingResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("waiting", waitingBody.GetProperty("data").GetProperty("status").GetString());

        var lineUserId = "line-user-bind-001";
        var webhookBody = $$"""
            {
              "events": [
                {
                  "type": "follow",
                  "source": { "userId": "{{lineUserId}}" },
                  "timestamp": 1735689600000
                }
              ]
            }
            """;
        var webhookRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/line/webhook")
        {
            Content = new StringContent(webhookBody, Encoding.UTF8, "application/json")
        };
        webhookRequest.Headers.Add("X-Line-Signature", ComputeSignature(webhookBody, "line-msg-test-secret"));
        var webhookResponse = await client.SendAsync(webhookRequest);
        webhookResponse.EnsureSuccessStatusCode();

        var readyResponse = await client.GetAsync($"/api/v1/account/line/bind/status?pendingBindingId={pendingId}");
        readyResponse.EnsureSuccessStatusCode();
        var readyBody = await readyResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ready", readyBody.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(lineUserId, readyBody.GetProperty("data").GetProperty("lineUserId").GetString());

        var confirmResponse = await client.PostAsJsonAsync(
            "/api/v1/account/line/bind/confirm",
            new { pendingBindingId = pendingId });
        confirmResponse.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var user = await db.AspNetUsers.FirstAsync(x => x.NormalizedUserName == "OTHER");
            Assert.Equal(lineUserId, user.LineMessagingApiUserId);
            Assert.NotNull(user.LineMessagingApiAuthorizedAt);
        }

        var unbindResponse = await client.PostAsync("/api/v1/account/line/bind/unbind", null);
        unbindResponse.EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var user = await db.AspNetUsers.FirstAsync(x => x.NormalizedUserName == "OTHER");
            Assert.Null(user.LineMessagingApiUserId);
            Assert.Null(user.LineMessagingApiAuthorizedAt);
        }
    }

    [Fact]
    public async Task LineWebhook_InvalidSignature_ReturnsUnauthorized()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var webhookBody = """
            {
              "events": []
            }
            """;
        var webhookRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/line/webhook")
        {
            Content = new StringContent(webhookBody, Encoding.UTF8, "application/json")
        };
        webhookRequest.Headers.Add("X-Line-Signature", "invalid-signature");

        var response = await client.SendAsync(webhookRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

    private static string ComputeSignature(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)));
    }
}
