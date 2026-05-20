using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NeighborGoods.Data;
using NeighborGoods.Data.Announcements;

namespace NeighborGoods.Api.Tests;

[Collection("ListingApiTests")]
public sealed class AnnouncementEndpointsTests(SqlServerContainerFixture fixture)
{
    private const string UserPassword = "Passw0rd!";
    private const string ConfirmedUserName = "tester";

    [Fact]
    public async Task GetActive_WhenNoAnnouncements_ReturnsEmptyItems()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/announcements/active");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.Equal(0, body.GetProperty("data").GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task GetActive_WhenEnabledAnnouncementExists_ReturnsItem()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        await SeedAnnouncementAsync(factory, "系統維護中", isEnabled: true);

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/announcements/active");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("data").GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("系統維護中", items[0].GetProperty("message").GetString());
    }

    [Fact]
    public async Task GetActive_WhenDisabledOrExpired_DoesNotReturnItem()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        await SeedAnnouncementAsync(factory, "已停用", isEnabled: false);
        await SeedAnnouncementAsync(
            factory,
            "已過期",
            endsAt: DateTime.UtcNow.AddHours(-1));

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/announcements/active");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("data").GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task GetActive_ScopeFilter_ReturnsGlobalAndMatchingScopeOnly()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        await SeedAnnouncementAsync(factory, "全站公告", scope: (byte)AnnouncementScope.Global);
        await SeedAnnouncementAsync(factory, "首頁公告", scope: (byte)AnnouncementScope.HomeOnly);

        using var client = factory.CreateClient();

        var homeResponse = await client.GetAsync("/api/v1/announcements/active?scope=1");
        homeResponse.EnsureSuccessStatusCode();
        var homeBody = await homeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var homeMessages = homeBody.GetProperty("data").GetProperty("items")
            .EnumerateArray()
            .Select(x => x.GetProperty("message").GetString())
            .ToHashSet();
        Assert.Contains("全站公告", homeMessages);
        Assert.Contains("首頁公告", homeMessages);

        var otherResponse = await client.GetAsync("/api/v1/announcements/active?scope=0");
        otherResponse.EnsureSuccessStatusCode();
        var otherBody = await otherResponse.Content.ReadFromJsonAsync<JsonElement>();
        var otherMessages = otherBody.GetProperty("data").GetProperty("items")
            .EnumerateArray()
            .Select(x => x.GetProperty("message").GetString())
            .ToHashSet();
        Assert.Contains("全站公告", otherMessages);
        Assert.DoesNotContain("首頁公告", otherMessages);
    }

    [Fact]
    public async Task AdminAnnouncements_NonAdmin_ReturnsForbidden()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var response = await client.GetAsync("/api/v1/admin/announcements");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminAnnouncements_Crud_SucceedsForAdmin()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ListingApiFactory.AdminUserName, UserPassword);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/announcements",
            new
            {
                message = "LINE 登入異常",
                severity = 2,
                scope = 0,
                sortOrder = 0,
                isEnabled = true,
                startsAt = (DateTime?)null,
                endsAt = (DateTime?)null,
                linkUrl = (string?)null,
                linkLabel = (string?)null,
            });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("data").GetProperty("id").GetGuid();

        var listResponse = await client.GetAsync("/api/v1/admin/announcements");
        listResponse.EnsureSuccessStatusCode();
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(listBody.GetProperty("data").GetProperty("items").GetArrayLength() >= 1);

        var patchResponse = await client.PatchAsJsonAsync(
            $"/api/v1/admin/announcements/{id}",
            new
            {
                message = "LINE 登入已恢復",
                severity = 1,
                scope = 0,
                sortOrder = 0,
                isEnabled = true,
                startsAt = (DateTime?)null,
                endsAt = (DateTime?)null,
                linkUrl = (string?)null,
                linkLabel = (string?)null,
            });
        patchResponse.EnsureSuccessStatusCode();

        var disableResponse = await client.PatchAsJsonAsync(
            $"/api/v1/admin/announcements/{id}/enabled",
            new { isEnabled = false });
        disableResponse.EnsureSuccessStatusCode();

        var deleteResponse = await client.DeleteAsync($"/api/v1/admin/announcements/{id}");
        deleteResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AdminCreateAnnouncement_WithInvalidSeverity_ReturnsBadRequest()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ListingApiFactory.AdminUserName, UserPassword);

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/announcements",
            new
            {
                message = "測試",
                severity = 99,
                scope = 0,
                sortOrder = 0,
                isEnabled = true,
                startsAt = (DateTime?)null,
                endsAt = (DateTime?)null,
                linkUrl = (string?)null,
                linkLabel = (string?)null,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<Guid> SeedAnnouncementAsync(
        ListingApiFactory factory,
        string message,
        byte severity = (byte)AnnouncementSeverity.Info,
        byte scope = (byte)AnnouncementScope.Global,
        int sortOrder = 0,
        bool isEnabled = true,
        DateTime? startsAt = null,
        DateTime? endsAt = null)
    {
        using var serviceScope = factory.Services.CreateScope();
        var db = serviceScope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var id = Guid.NewGuid();
        db.SiteAnnouncements.Add(new SiteAnnouncement
        {
            Id = id,
            Message = message,
            Severity = severity,
            Scope = scope,
            SortOrder = sortOrder,
            IsEnabled = isEnabled,
            StartsAt = startsAt,
            EndsAt = endsAt,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return id;
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
