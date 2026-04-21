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
using NeighborGoods.Api.Infrastructure.Storage;
using NeighborGoods.Api.Shared.Notifications;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;
using NeighborGoods.Api.Shared.Persistence;

namespace NeighborGoods.Api.Tests;

internal sealed class ListingApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    private readonly string _connectionString = connectionString;
    private const string ConfirmedUserId = "test-user-confirmed";
    private const string UnconfirmedUserId = "test-user-unconfirmed";
    private const string OtherConfirmedUserId = "test-user-other";
    private const string AdminUserId = "test-user-admin";
    private const string ConfirmedUserName = "tester";
    private const string UnconfirmedUserName = "novalid";
    internal const string OtherConfirmedUserName = "other";
    internal const string AdminUserName = "admin";
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
                new KeyValuePair<string, string?>("Line:CallbackUrl", "https://localhost/api/v1/auth/line/callback"),
                new KeyValuePair<string, string?>("LineMessagingApi:ChannelAccessToken", "line-msg-test-token"),
                new KeyValuePair<string, string?>("LineMessagingApi:ChannelSecret", "line-msg-test-secret"),
                new KeyValuePair<string, string?>("LineMessagingApi:BotId", "@bot_test")
            ]);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILineOAuthClient>();
            services.AddSingleton<ILineOAuthClient, FakeLineOAuthClient>();
            services.RemoveAll<IBlobStorage>();
            services.AddSingleton<IBlobStorage, FakeBlobStorage>();
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender, FakeEmailSender>();
            services.RemoveAll<ILineMessageSender>();
            services.AddSingleton<ILineMessageSender, FakeLineMessageSender>();

            var connectionBuilder = new SqlConnectionStringBuilder(_connectionString);
            var dbName = connectionBuilder.InitialCatalog;
            Console.WriteLine($"[ListingApiTests] Using test database: {dbName} on {connectionBuilder.DataSource}");

            if (connectionBuilder.DataSource.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Test database must not point to Azure SQL.");
            }

            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            FakeEmailSender.Reset();
            FakeLineMessageSender.Reset();
            dbContext.Database.Migrate();
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [EmailVerificationChallenges]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [PurchaseRequests]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [Messages]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [Reviews]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [Conversations]");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM [ListingFavorites]");
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

            dbContext.AspNetUsers.Add(BuildUser(
                OtherConfirmedUserId,
                OtherConfirmedUserName,
                "other@example.com",
                emailConfirmed: true));

            dbContext.AspNetUsers.Add(BuildUser(
                AdminUserId,
                AdminUserName,
                "admin@example.com",
                emailConfirmed: true,
                role: 3));

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
            dbContext.ListingImages.Add(new ListingImage
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                ListingId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ImageUrl = "https://legacy-images.test/listings/11111111-1111-1111-1111-111111111111/0-old.jpg",
                SortOrder = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            });
            dbContext.ListingImages.Add(new ListingImage
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                ListingId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ImageUrl = "listings/11111111-1111-1111-1111-111111111111/1-new-path.jpg",
                SortOrder = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });

            // 累加篩選測試用（標題含 filter-test- 前綴；皆為上架中）
            dbContext.Listings.Add(new global::NeighborGoods.Api.Features.Listing.Listing
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Title = "filter-test-charity-only",
                Description = "",
                Price = 100,
                IsFree = false,
                IsCharity = true,
                SellerId = ConfirmedUserId,
                Category = 1,
                PickupLocation = 3,
                Condition = 1,
                BuyerId = null,
                Residence = 2,
                IsTradeable = false,
                IsPinned = false,
                Status = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            });
            dbContext.Listings.Add(new global::NeighborGoods.Api.Features.Listing.Listing
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Title = "filter-test-free-only",
                Description = "",
                Price = 0,
                IsFree = true,
                IsCharity = false,
                SellerId = ConfirmedUserId,
                Category = 1,
                PickupLocation = 3,
                Condition = 1,
                BuyerId = null,
                Residence = 2,
                IsTradeable = false,
                IsPinned = false,
                Status = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            });
            dbContext.Listings.Add(new global::NeighborGoods.Api.Features.Listing.Listing
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Title = "filter-test-both-flags",
                Description = "",
                Price = 0,
                IsFree = true,
                IsCharity = true,
                SellerId = ConfirmedUserId,
                Category = 1,
                PickupLocation = 3,
                Condition = 1,
                BuyerId = null,
                Residence = 2,
                IsTradeable = false,
                IsPinned = false,
                Status = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            });
            dbContext.Listings.Add(new global::NeighborGoods.Api.Features.Listing.Listing
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Title = "filter-test-tradeable-only",
                Description = "",
                Price = 200,
                IsFree = false,
                IsCharity = false,
                SellerId = ConfirmedUserId,
                Category = 1,
                PickupLocation = 3,
                Condition = 1,
                BuyerId = null,
                Residence = 2,
                IsTradeable = true,
                IsPinned = false,
                Status = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            });
            dbContext.SaveChanges();
        });
    }

    private static AspNetUser BuildUser(string id, string userName, string email, bool emailConfirmed, int role = 0)
    {
        var user = new AspNetUser
        {
            Id = id,
            DisplayName = userName,
            Role = role,
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
            EmailNotificationEnabled = emailConfirmed,
            TopPinCredits = emailConfirmed ? 5 : 0
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

    private sealed class FakeBlobStorage : IBlobStorage
    {
        public string BuildPublicUrl(string blobName) =>
            $"https://blob.local.test/listing/{blobName.TrimStart('/')}";

        public Task DeleteAsync(string blobName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string> UploadCompressedJpegAsync(
            string blobName,
            Stream content,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(blobName);
    }
}
