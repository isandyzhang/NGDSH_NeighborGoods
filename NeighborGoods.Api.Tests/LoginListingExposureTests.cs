using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeighborGoods.Data;
using NeighborGoods.Data.Listings;

namespace NeighborGoods.Api.Tests;

[Collection("ListingApiTests")]
public sealed class LoginListingExposureTests
{
    private const string OtherUserId = "test-user-other";
    private const string OtherUserName = ListingApiFactory.OtherConfirmedUserName;
    private const string UserPassword = "Passw0rd!";

    private readonly SqlServerContainerFixture _fixture;

    public LoginListingExposureTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Login_BoostsOldestActiveUnpinned_AndKeepsListedAt()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var reservedId = Guid.NewGuid();
        var oldestId = Guid.NewGuid();
        var newerId = Guid.NewGuid();
        var oldestListedAt = DateTime.UtcNow.AddDays(-10);
        await SeedListingAsync(factory, reservedId, OtherUserId, DateTime.UtcNow.AddDays(-12), ListingStatus.Reserved);
        await SeedListingAsync(factory, oldestId, OtherUserId, oldestListedAt);
        await SeedListingAsync(factory, newerId, OtherUserId, DateTime.UtcNow.AddDays(-5));

        var loginResponse = await LoginAsync(client, OtherUserName);
        loginResponse.EnsureSuccessStatusCode();

        var oldest = await GetListingAsync(factory, oldestId);
        var newer = await GetListingAsync(factory, newerId);
        var reserved = await GetListingAsync(factory, reservedId);

        Assert.NotNull(oldest.LastExposedAt);
        Assert.Null(newer.LastExposedAt);
        Assert.Null(reserved.LastExposedAt);
        Assert.Equal(oldestListedAt, oldest.ListedAt, TimeSpan.FromSeconds(1));

        var items = await GetListingIdsAsync(client);
        Assert.Equal(oldestId, items[0]);
    }

    [Fact]
    public async Task Login_WithinCooldown_DoesNotBoostAgain()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var oldestId = Guid.NewGuid();
        var newerId = Guid.NewGuid();
        await SeedListingAsync(factory, oldestId, OtherUserId, DateTime.UtcNow.AddDays(-10));
        await SeedListingAsync(factory, newerId, OtherUserId, DateTime.UtcNow.AddDays(-8));

        (await LoginAsync(client, OtherUserName)).EnsureSuccessStatusCode();
        var firstBoostedAt = (await GetListingAsync(factory, oldestId)).LastExposedAt;
        Assert.NotNull(firstBoostedAt);

        (await LoginAsync(client, OtherUserName)).EnsureSuccessStatusCode();

        var oldest = await GetListingAsync(factory, oldestId);
        var newer = await GetListingAsync(factory, newerId);
        Assert.NotNull(oldest.LastExposedAt);
        Assert.Equal(firstBoostedAt!.Value, oldest.LastExposedAt.Value, TimeSpan.FromSeconds(1));
        Assert.Null(newer.LastExposedAt);
    }

    [Fact]
    public async Task Login_SkipsEffectivelyPinnedListing()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var pinnedId = Guid.NewGuid();
        var unpinnedId = Guid.NewGuid();
        await SeedListingAsync(
            factory,
            pinnedId,
            OtherUserId,
            DateTime.UtcNow.AddDays(-10),
            isPinned: true,
            pinnedEndDate: DateTime.UtcNow.AddDays(2));
        await SeedListingAsync(factory, unpinnedId, OtherUserId, DateTime.UtcNow.AddDays(-8));

        (await LoginAsync(client, OtherUserName)).EnsureSuccessStatusCode();

        var pinned = await GetListingAsync(factory, pinnedId);
        var unpinned = await GetListingAsync(factory, unpinnedId);
        Assert.Null(pinned.LastExposedAt);
        Assert.NotNull(unpinned.LastExposedAt);

        var items = await GetListingIdsAsync(client);
        Assert.Equal(pinnedId, items[0]);
        Assert.Equal(unpinnedId, items[1]);
    }

    [Fact]
    public async Task Refresh_DoesNotBoostListing()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var oldestId = Guid.NewGuid();
        var newerId = Guid.NewGuid();
        await SeedListingAsync(factory, oldestId, OtherUserId, DateTime.UtcNow.AddDays(-10));
        await SeedListingAsync(factory, newerId, OtherUserId, DateTime.UtcNow.AddDays(-8));

        var loginResponse = await LoginAsync(client, OtherUserName);
        loginResponse.EnsureSuccessStatusCode();
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = loginBody.GetProperty("data").GetProperty("refreshToken").GetString();

        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var newer = await GetListingAsync(factory, newerId);
        Assert.Null(newer.LastExposedAt);
        Assert.NotNull((await GetListingAsync(factory, oldestId)).LastExposedAt);
    }

    [Fact]
    public async Task Login_DoesNotBoostListingNewerThanMinAge()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var listingId = Guid.NewGuid();
        await SeedListingAsync(factory, listingId, OtherUserId, DateTime.UtcNow.AddHours(-1));

        (await LoginAsync(client, OtherUserName)).EnsureSuccessStatusCode();

        var listing = await GetListingAsync(factory, listingId);
        Assert.Null(listing.LastExposedAt);
    }

    [Fact]
    public async Task LineCallback_BoostsOldestListing()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var listingId = Guid.NewGuid();
        await SeedListingAsync(factory, listingId, OtherUserId, DateTime.UtcNow.AddDays(-9));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var user = await db.AspNetUsers.FirstAsync(x => x.Id == OtherUserId);
            user.LineUserId = "line-user-001";
            await db.SaveChangesAsync();
        }

        var loginRedirectResponse = await client.GetAsync("/api/v1/auth/line/login");
        Assert.Equal(HttpStatusCode.Redirect, loginRedirectResponse.StatusCode);
        var location = loginRedirectResponse.Headers.Location;
        Assert.NotNull(location);
        var state = ExtractQueryParameter(location, "state");
        Assert.False(string.IsNullOrWhiteSpace(state));

        var callbackResponse = await client.GetAsync(
            $"/api/v1/auth/line/callback?code=line-ok&state={Uri.EscapeDataString(state!)}");
        Assert.Equal(HttpStatusCode.OK, callbackResponse.StatusCode);

        var listing = await GetListingAsync(factory, listingId);
        Assert.NotNull(listing.LastExposedAt);
        Assert.Equal(listingId, (await GetListingIdsAsync(client))[0]);
    }

    private static async Task SeedListingAsync(
        ListingApiFactory factory,
        Guid id,
        string sellerId,
        DateTime listedAt,
        ListingStatus status = ListingStatus.Active,
        bool isPinned = false,
        DateTime? pinnedEndDate = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        db.Listings.Add(new Listing
        {
            Id = id,
            Title = $"login-exposure-{id:N}",
            Description = "login exposure test",
            Price = 100,
            SellerId = sellerId,
            Category = 1,
            PickupLocation = 3,
            Condition = 1,
            Residence = 2,
            IsPinned = isPinned,
            PinnedEndDate = pinnedEndDate,
            PinnedStartDate = isPinned ? listedAt : null,
            Status = (int)status,
            ListedAt = listedAt,
            CreatedAt = listedAt,
            UpdatedAt = listedAt
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Listing> GetListingAsync(ListingApiFactory factory, Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        return await db.Listings.AsNoTracking().FirstAsync(x => x.Id == id);
    }

    private static async Task<List<Guid>> GetListingIdsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/listings?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("data").GetProperty("items")
            .EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .ToList();
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string userName) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userNameOrEmail = userName,
            password = UserPassword
        });

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
