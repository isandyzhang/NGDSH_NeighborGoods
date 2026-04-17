using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NeighborGoods.Api.Features.Auth.Services;
using NeighborGoods.Api.Features.Listing;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;
using NeighborGoods.Api.Shared.Persistence;

namespace NeighborGoods.Api.Tests;

internal sealed class ListingApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    private readonly string _connectionString = connectionString;
    private const string ConfirmedUserId = "test-user-confirmed";
    private const string UnconfirmedUserId = "test-user-unconfirmed";
    private const string ConfirmedUserName = "tester";
    private const string UnconfirmedUserName = "novalid";
    private const string UserPassword = "Passw0rd!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", _connectionString),
                new KeyValuePair<string, string?>("Line:ChannelId", "line-test-channel"),
                new KeyValuePair<string, string?>("Line:ChannelSecret", "line-test-channel-secret"),
                new KeyValuePair<string, string?>("Line:CallbackUrl", "https://localhost/api/v1/auth/line/callback")
            ]);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILineOAuthClient>();
            services.AddSingleton<ILineOAuthClient, FakeLineOAuthClient>();

            var connectionBuilder = new SqlConnectionStringBuilder(_connectionString);
            var dbName = connectionBuilder.InitialCatalog;
            Console.WriteLine($"[ListingApiTests] Using test database: {dbName} on {connectionBuilder.DataSource}");

            if (connectionBuilder.DataSource.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Test database must not point to Azure SQL.");
            }

            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            dbContext.Database.Migrate();
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [Messages]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [Reviews]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [Conversations]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [ListingImages]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [ListingTopSubmissions]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [AdminMessages]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [LineBindingPending]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [Listings]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [AspNetUserRoles]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [AspNetUserClaims]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [AspNetUserLogins]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [AspNetUserTokens]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [AspNetRoleClaims]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [AspNetRoles]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [AspNetUsers]");

            dbContext.AspNetUsers.Add(BuildUser(
                ConfirmedUserId,
                ConfirmedUserName,
                "tester@example.com",
                emailConfirmed: true));

            dbContext.AspNetUsers.Add(BuildUser(
                UnconfirmedUserId,
                UnconfirmedUserName,
                "novalid@example.com",
                emailConfirmed: false));

            dbContext.Listings.Add(new global::NeighborGoods.Api.Features.Listing.Listing
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Title = "二手書櫃",
                Description = "九成新",
                Price = 500,
                IsFree = false,
                IsCharity = false,
                SellerId = ConfirmedUserId,
                Category = 0,
                PickupLocation = 3,
                Condition = 1,
                BuyerId = null,
                Residence = 2,
                IsTradeable = false,
                IsPinned = false,
                Status = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-2)
            });
            dbContext.SaveChanges();
        });
    }

    private static AspNetUser BuildUser(string id, string userName, string email, bool emailConfirmed)
    {
        var user = new AspNetUser
        {
            Id = id,
            DisplayName = userName,
            Role = 0,
            CreatedAt = DateTime.UtcNow.AddYears(-1),
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = emailConfirmed,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            PhoneNumberConfirmed = false,
            TwoFactorEnabled = false,
            LockoutEnabled = false,
            AccessFailedCount = 0,
            LineNotificationPreference = 0,
            EmailNotificationEnabled = false,
            TopPinCredits = 0
        };

        var hasher = new PasswordHasher<AspNetUser>();
        user.PasswordHash = hasher.HashPassword(user, UserPassword);
        return user;
    }

    private sealed class FakeLineOAuthClient : ILineOAuthClient
    {
        public string BuildAuthorizeUrl(string state) =>
            $"https://line.test/oauth?state={Uri.EscapeDataString(state)}";

        public Task<LineOAuthProfile?> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            if (code == "line-ok")
            {
                return Task.FromResult<LineOAuthProfile?>(new LineOAuthProfile("line-user-001", "Line 測試用戶"));
            }

            return Task.FromResult<LineOAuthProfile?>(null);
        }
    }
}
