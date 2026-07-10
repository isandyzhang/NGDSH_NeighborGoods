using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeighborGoods.Data;

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
        var data = body.GetProperty("data");
        Assert.Equal("tester", data.GetProperty("displayName").GetString());
        Assert.True(data.GetProperty("statistics").GetProperty("totalListings").GetInt32() >= 1);
        Assert.True(data.GetProperty("emailNotificationEnabled").GetBoolean());
    }

    [Fact]
    public async Task NotificationsDisable_ClearsEmailAndLineFlags()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "other@example.com", UserPassword);

        var patchResponse = await client.PatchAsJsonAsync(
            "/api/v1/account/line/preferences",
            new
            {
                marketingPushEnabled = true,
                preferenceNewListings = true,
                preferencePriceDrop = false,
                preferenceMessageDigest = true
            });
        patchResponse.EnsureSuccessStatusCode();

        var disableResponse = await client.PostAsync("/api/v1/account/notifications/disable", null);
        disableResponse.EnsureSuccessStatusCode();
        var disableBody = await disableResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(disableBody.GetProperty("data").GetProperty("emailNotificationEnabled").GetBoolean());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var user = await db.AspNetUsers.FirstAsync(x => x.NormalizedUserName == "OTHER");
            Assert.False(user.EmailNotificationEnabled);
            Assert.Equal(0, user.LineNotificationPreference);
        }

        var meResponse = await client.GetAsync("/api/v1/account/me");
        meResponse.EnsureSuccessStatusCode();
        var meBody = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(meBody.GetProperty("data").GetProperty("emailNotificationEnabled").GetBoolean());
    }

    [Fact]
    public async Task NotificationsEmailDisable_AndEnable_UpdatesFlag()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "tester@example.com", UserPassword);

        var disableResponse = await client.PostAsync("/api/v1/account/notifications/email/disable", null);
        disableResponse.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var user = await db.AspNetUsers.FirstAsync(x => x.NormalizedUserName == "TESTER");
            Assert.False(user.EmailNotificationEnabled);
        }

        var enableResponse = await client.PostAsync("/api/v1/account/notifications/email/enable", null);
        enableResponse.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var user = await db.AspNetUsers.FirstAsync(x => x.NormalizedUserName == "TESTER");
            Assert.True(user.EmailNotificationEnabled);
        }
    }

    [Fact]
    public async Task NotificationsLineDisable_ClearsPreference()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "other@example.com", UserPassword);

        var patchResponse = await client.PatchAsJsonAsync(
            "/api/v1/account/line/preferences",
            new
            {
                marketingPushEnabled = true,
                preferenceNewListings = true,
                preferencePriceDrop = false,
                preferenceMessageDigest = false
            });
        patchResponse.EnsureSuccessStatusCode();

        var disableLineResponse = await client.PostAsync("/api/v1/account/notifications/line/disable", null);
        disableLineResponse.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var user = await db.AspNetUsers.FirstAsync(x => x.NormalizedUserName == "OTHER");
        Assert.Equal(0, user.LineNotificationPreference);
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
    public async Task LineBinding_LiffComplete_WithoutAuth_ReturnsUnauthorized()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "other@example.com", UserPassword);

        var startResponse = await client.PostAsync("/api/v1/account/line/bind/start", null);
        startResponse.EnsureSuccessStatusCode();
        var startBody = await startResponse.Content.ReadFromJsonAsync<JsonElement>();
        var data = startBody.GetProperty("data");
        var bindingToken = data.GetProperty("bindingToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(bindingToken));
        var liffUrl = data.GetProperty("liffUrl").GetString();
        Assert.False(string.IsNullOrWhiteSpace(liffUrl));
        Assert.Contains("bindToken=", liffUrl!, StringComparison.Ordinal);
        Assert.StartsWith("https://liff.line.me/", liffUrl!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("liff.state=", liffUrl!, StringComparison.Ordinal);
        Assert.DoesNotContain("%3FbindToken", liffUrl!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(bindingToken!, liffUrl!, StringComparison.Ordinal);

        using var anon = factory.CreateClient();
        var completeResponse = await anon.PostAsJsonAsync(
            "/api/v1/account/line/bind/liff-complete",
            new { bindingToken, idToken = FakeLineLiffIdTokenVerifier.ValidTestIdToken });
        Assert.Equal(HttpStatusCode.Unauthorized, completeResponse.StatusCode);
    }

    [Fact]
    public async Task LineBinding_LiffComplete_ForDifferentUser_ReturnsForbidden()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var ownerClient = factory.CreateClient();
        await AuthenticateAsAsync(ownerClient, "other@example.com", UserPassword);

        var startResponse = await ownerClient.PostAsync("/api/v1/account/line/bind/start", null);
        startResponse.EnsureSuccessStatusCode();
        var startBody = await startResponse.Content.ReadFromJsonAsync<JsonElement>();
        var bindingToken = startBody.GetProperty("data").GetProperty("bindingToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(bindingToken));

        using var wrongUserClient = factory.CreateClient();
        await AuthenticateAsAsync(wrongUserClient, "tester@example.com", UserPassword);
        var completeResponse = await wrongUserClient.PostAsJsonAsync(
            "/api/v1/account/line/bind/liff-complete",
            new { bindingToken, idToken = FakeLineLiffIdTokenVerifier.ValidTestIdToken });
        Assert.Equal(HttpStatusCode.Forbidden, completeResponse.StatusCode);

        var body = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LINE_BIND_USER_MISMATCH", body.GetProperty("error").GetProperty("code").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var owner = await db.AspNetUsers.FirstAsync(x => x.NormalizedUserName == "OTHER");
        var wrongUser = await db.AspNetUsers.FirstAsync(x => x.NormalizedUserName == "TESTER");
        Assert.Null(owner.LineMessagingApiUserId);
        Assert.Null(wrongUser.LineMessagingApiUserId);
    }

    [Fact]
    public async Task LineBinding_LiffCompleteAndUnbind_WorksEndToEnd()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "other@example.com", UserPassword);

        var startResponse = await client.PostAsync("/api/v1/account/line/bind/start", null);
        startResponse.EnsureSuccessStatusCode();
        var startBody = await startResponse.Content.ReadFromJsonAsync<JsonElement>();
        var data = startBody.GetProperty("data");
        var bindingToken = data.GetProperty("bindingToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(bindingToken));
        var liffUrl = data.GetProperty("liffUrl").GetString();
        Assert.False(string.IsNullOrWhiteSpace(liffUrl));
        Assert.Contains("bindToken=", liffUrl!, StringComparison.Ordinal);
        Assert.Contains("liff.state=", liffUrl!, StringComparison.Ordinal);
        Assert.Contains("%2Fliff%2Fline-notify", liffUrl!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("%3FbindToken", liffUrl!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(bindingToken!, liffUrl!, StringComparison.Ordinal);

        var completeResponse = await client.PostAsJsonAsync(
            "/api/v1/account/line/bind/liff-complete",
            new { bindingToken, idToken = FakeLineLiffIdTokenVerifier.ValidTestIdToken });
        completeResponse.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var user = await db.AspNetUsers.FirstAsync(x => x.NormalizedUserName == "OTHER");
            Assert.Equal(FakeLineLiffIdTokenVerifier.ValidTestSub, user.LineMessagingApiUserId);
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

    [Fact]
    public async Task LineWebhook_PostbackMyMessages_RepliesFlexMessage()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        const string lineUserId = "line-user-webhook-001";

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var user = await db.AspNetUsers.FirstAsync(x => x.NormalizedUserName == "OTHER");
            user.LineMessagingApiUserId = lineUserId;
            user.LineMessagingApiAuthorizedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var webhookBody = $$"""
            {
              "events": [
                {
                  "type": "postback",
                  "replyToken": "reply-token-1",
                  "source": { "userId": "{{lineUserId}}" },
                  "postback": { "data": "action=myMessages" }
                }
              ]
            }
            """;
        var webhookRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/line/webhook")
        {
            Content = new StringContent(webhookBody, Encoding.UTF8, "application/json")
        };
        webhookRequest.Headers.Add("X-Line-Signature", ComputeSignature(webhookBody, "line-msg-test-secret"));

        var response = await client.SendAsync(webhookRequest);
        response.EnsureSuccessStatusCode();

        Assert.Single(FakeLineMessageSender.ReplyFlexMessages);
        Assert.Contains("我的訊息", FakeLineMessageSender.ReplyFlexMessages[0].AltText);
    }

    [Fact]
    public async Task LinePreferences_PatchThenGet_ReturnsUpdatedFlags()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "other@example.com", UserPassword);

        var patchResponse = await client.PatchAsJsonAsync(
            "/api/v1/account/line/preferences",
            new
            {
                marketingPushEnabled = true,
                preferenceNewListings = true,
                preferencePriceDrop = false,
                preferenceMessageDigest = true
            });
        patchResponse.EnsureSuccessStatusCode();

        var getResponse = await client.GetAsync("/api/v1/account/line/preferences");
        getResponse.EnsureSuccessStatusCode();
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var data = body.GetProperty("data");
        Assert.True(data.GetProperty("marketingPushEnabled").GetBoolean());
        Assert.True(data.GetProperty("preferenceNewListings").GetBoolean());
        Assert.False(data.GetProperty("preferencePriceDrop").GetBoolean());
        Assert.True(data.GetProperty("preferenceMessageDigest").GetBoolean());
    }

    [Fact]
    public async Task LineQuota_Get_ReturnsQuotaPayload()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "other@example.com", UserPassword);

        var response = await client.GetAsync("/api/v1/account/line/quota");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = body.GetProperty("data");
        Assert.True(data.TryGetProperty("usedCount", out _));
        Assert.True(data.TryGetProperty("note", out _));
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
