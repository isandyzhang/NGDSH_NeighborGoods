using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeighborGoods.Api.Features.Listing;
using NeighborGoods.Api.Features.PurchaseRequests;
using NeighborGoods.Api.Features.PurchaseRequests.Services;
using NeighborGoods.Data;
using NeighborGoods.Data.LegacyEntities;

namespace NeighborGoods.Api.Tests;

[Collection("ListingApiTests")]
public sealed class PurchaseRequestEndpointsTests(SqlServerContainerFixture fixture)
{
    private static readonly Guid SeededListingId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string SellerUserId = "test-user-confirmed";
    private const string BuyerUserId = "test-user-other";
    private const string UserPassword = "Passw0rd!";

    [Fact]
    public async Task CreatePurchaseRequest_ReturnsPendingWith12HoursWindow()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "other@example.com", UserPassword);

        var response = await client.PostAsync($"/api/v1/listings/{SeededListingId}/purchase-requests", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        var data = body.GetProperty("data");
        Assert.Equal(SeededListingId, data.GetProperty("listingId").GetGuid());
        Assert.Equal((int)PurchaseRequestStatus.Pending, data.GetProperty("status").GetInt32());
        Assert.True(data.GetProperty("remainingSeconds").GetInt32() > 0);

        var expireAt = data.GetProperty("expireAt").GetDateTime();
        var createdAt = data.GetProperty("createdAt").GetDateTime();
        var diff = expireAt - createdAt;
        Assert.True(diff.TotalHours is > 11.9 and < 12.1);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var conversationId = data.GetProperty("conversationId").GetGuid();
        var systemMessage = await db.Messages
            .Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Content)
            .FirstAsync();
        Assert.Equal("[系統發送]買家已送出購買請求，請於 12 小時內回覆。", systemMessage);
    }

    [Fact]
    public async Task CreatePurchaseRequest_WhenAlreadyPending_ReturnsConflict()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "other@example.com", UserPassword);

        var first = await client.PostAsync($"/api/v1/listings/{SeededListingId}/purchase-requests", null);
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsync($"/api/v1/listings/{SeededListingId}/purchase-requests", null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        var error = body.GetProperty("error");
        Assert.Equal("PURCHASE_REQUEST_ALREADY_PENDING", error.GetProperty("code").GetString());
        Assert.Equal("此商品已有待回覆的交易請求", error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task AcceptPurchaseRequest_BySeller_SetsListingReserved()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var buyerClient = factory.CreateClient();
        await AuthenticateAsAsync(buyerClient, "other@example.com", UserPassword);

        var create = await buyerClient.PostAsync($"/api/v1/listings/{SeededListingId}/purchase-requests", null);
        create.EnsureSuccessStatusCode();
        var requestId = (await create.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data")
            .GetProperty("id")
            .GetGuid();

        using var sellerClient = factory.CreateClient();
        await AuthenticateAsAsync(sellerClient, "tester@example.com", UserPassword);
        var accept = await sellerClient.PostAsync($"/api/v1/purchase-requests/{requestId}/accept", null);

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var body = await accept.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)PurchaseRequestStatus.Accepted, body.GetProperty("data").GetProperty("status").GetInt32());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var listing = await db.Listings.FindAsync(SeededListingId);
        Assert.NotNull(listing);
        Assert.Equal((int)ListingStatus.Reserved, listing!.Status);
    }

    [Fact]
    public async Task CancelPurchaseRequest_ByBuyer_ChangesStatusToCancelled()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "other@example.com", UserPassword);

        var create = await client.PostAsync($"/api/v1/listings/{SeededListingId}/purchase-requests", null);
        create.EnsureSuccessStatusCode();
        var requestId = (await create.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data")
            .GetProperty("id")
            .GetGuid();

        var cancel = await client.PostAsync($"/api/v1/purchase-requests/{requestId}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var body = await cancel.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)PurchaseRequestStatus.Cancelled, body.GetProperty("data").GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task GetPurchaseRequest_WhenExpired_AutoMarksExpired()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var now = DateTime.UtcNow;
            var conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                ListingId = SeededListingId,
                Participant1Id = BuyerUserId,
                Participant2Id = SellerUserId,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Conversations.Add(conversation);
            db.PurchaseRequests.Add(new PurchaseRequest
            {
                Id = Guid.NewGuid(),
                ListingId = SeededListingId,
                ConversationId = conversation.Id,
                BuyerId = BuyerUserId,
                SellerId = SellerUserId,
                Status = (int)PurchaseRequestStatus.Pending,
                CreatedAt = now.AddHours(-13),
                ExpireAt = now.AddHours(-1)
            });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, "other@example.com", UserPassword);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var requestId = await dbContext.PurchaseRequests
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Id)
            .FirstAsync();

        var response = await client.GetAsync($"/api/v1/purchase-requests/{requestId}");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)PurchaseRequestStatus.Expired, body.GetProperty("data").GetProperty("status").GetInt32());

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var request = await verifyDb.PurchaseRequests.FindAsync(requestId);
        Assert.NotNull(request);
        Assert.Equal((int)PurchaseRequestStatus.Expired, request!.Status);
    }

    [Fact]
    public async Task SendSellerReminder_WithBoundLineUser_SendsFlexPushAndUpdatesLastSentAt()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        var now = DateTime.UtcNow;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var seller = await db.AspNetUsers.FirstAsync(x => x.Id == SellerUserId);
            seller.LineMessagingApiUserId = "line-seller-001";
            seller.LineMessagingApiAuthorizedAt = now.AddDays(-1);
            seller.LineNotificationLastSentAt = now.AddDays(-2);

            var conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                ListingId = SeededListingId,
                Participant1Id = BuyerUserId,
                Participant2Id = SellerUserId,
                CreatedAt = now.AddHours(-2),
                UpdatedAt = now.AddHours(-2)
            };
            db.Conversations.Add(conversation);
            db.PurchaseRequests.Add(new PurchaseRequest
            {
                Id = Guid.NewGuid(),
                ListingId = SeededListingId,
                ConversationId = conversation.Id,
                BuyerId = BuyerUserId,
                SellerId = SellerUserId,
                Status = (int)PurchaseRequestStatus.Pending,
                CreatedAt = now.AddHours(-11),
                ExpireAt = now.AddMinutes(30),
                SellerReminderSentAt = null
            });

            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<PurchaseRequestService>();
            var reminded = await service.SendSellerReminderAsync();
            Assert.True(reminded >= 1);
        }

        Assert.NotEmpty(FakeLineMessageSender.PushFlexMessages);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var seller = await db.AspNetUsers.FirstAsync(x => x.Id == SellerUserId);
            Assert.NotNull(seller.LineNotificationLastSentAt);
        }
    }

    [Fact]
    public async Task SendSellerReminder_WhenCooldownNotMet_DoesNotSendLinePush()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        var now = DateTime.UtcNow;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var seller = await db.AspNetUsers.FirstAsync(x => x.Id == SellerUserId);
            seller.LineMessagingApiUserId = "line-seller-002";
            seller.LineMessagingApiAuthorizedAt = now.AddDays(-1);
            seller.LineNotificationLastSentAt = now.AddMinutes(-10);

            var conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                ListingId = SeededListingId,
                Participant1Id = BuyerUserId,
                Participant2Id = SellerUserId,
                CreatedAt = now.AddHours(-2),
                UpdatedAt = now.AddHours(-2)
            };
            db.Conversations.Add(conversation);
            db.PurchaseRequests.Add(new PurchaseRequest
            {
                Id = Guid.NewGuid(),
                ListingId = SeededListingId,
                ConversationId = conversation.Id,
                BuyerId = BuyerUserId,
                SellerId = SellerUserId,
                Status = (int)PurchaseRequestStatus.Pending,
                CreatedAt = now.AddHours(-11),
                ExpireAt = now.AddMinutes(30),
                SellerReminderSentAt = null
            });

            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<PurchaseRequestService>();
            await service.SendSellerReminderAsync();
        }

        Assert.Empty(FakeLineMessageSender.PushFlexMessages);
    }

    [Theory]
    [InlineData(false, false, false, ListingStatus.Sold)]
    [InlineData(true, true, false, ListingStatus.Donated)]
    [InlineData(false, false, true, ListingStatus.GivenOrTraded)]
    public async Task CompleteBySeller_UpdatesListingToExpectedTerminalStatus(
        bool isFree,
        bool isCharity,
        bool isTradeable,
        ListingStatus expectedStatus)
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        var listingId = await SeedListingAsync(factory, isFree, isCharity, isTradeable);

        using var buyerClient = factory.CreateClient();
        await AuthenticateAsAsync(buyerClient, "other@example.com", UserPassword);
        var createResponse = await buyerClient.PostAsync($"/api/v1/listings/{listingId}/purchase-requests", null);
        createResponse.EnsureSuccessStatusCode();
        var createData = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var conversationId = createData.GetProperty("conversationId").GetGuid();

        using var sellerClient = factory.CreateClient();
        await AuthenticateAsAsync(sellerClient, "tester@example.com", UserPassword);
        var acceptResponse = await sellerClient.PostAsync($"/api/v1/conversations/{conversationId}/purchase-request/accept", null);
        acceptResponse.EnsureSuccessStatusCode();

        var completeResponse = await sellerClient.PostAsync(
            $"/api/v1/conversations/{conversationId}/purchase-request/complete-by-seller",
            null);
        completeResponse.EnsureSuccessStatusCode();
        var completeBody = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)PurchaseRequestStatus.SellerMarkedCompleted, completeBody.GetProperty("data").GetProperty("status").GetInt32());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var listing = await db.Listings.FindAsync(listingId);
        Assert.NotNull(listing);
        Assert.Equal((int)expectedStatus, listing!.Status);
    }

    [Fact]
    public async Task ConfirmReceivedByBuyer_RequiresSellerCompletionThenTransitionsToCompleted()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        var listingId = await SeedListingAsync(factory, false, false, false);

        using var buyerClient = factory.CreateClient();
        await AuthenticateAsAsync(buyerClient, "other@example.com", UserPassword);
        var createResponse = await buyerClient.PostAsync($"/api/v1/listings/{listingId}/purchase-requests", null);
        createResponse.EnsureSuccessStatusCode();
        var createData = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var conversationId = createData.GetProperty("conversationId").GetGuid();

        using var sellerClient = factory.CreateClient();
        await AuthenticateAsAsync(sellerClient, "tester@example.com", UserPassword);
        var acceptResponse = await sellerClient.PostAsync($"/api/v1/conversations/{conversationId}/purchase-request/accept", null);
        acceptResponse.EnsureSuccessStatusCode();

        var earlyConfirm = await buyerClient.PostAsync(
            $"/api/v1/conversations/{conversationId}/purchase-request/confirm-received",
            null);
        Assert.Equal(HttpStatusCode.Conflict, earlyConfirm.StatusCode);
        var earlyError = await earlyConfirm.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            "PURCHASE_REQUEST_INVALID_STATE",
            earlyError.GetProperty("error").GetProperty("code").GetString());

        var completeResponse = await sellerClient.PostAsync(
            $"/api/v1/conversations/{conversationId}/purchase-request/complete-by-seller",
            null);
        completeResponse.EnsureSuccessStatusCode();

        var confirmResponse = await buyerClient.PostAsync(
            $"/api/v1/conversations/{conversationId}/purchase-request/confirm-received",
            null);
        confirmResponse.EnsureSuccessStatusCode();
        var confirmBody = await confirmResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)PurchaseRequestStatus.Completed, confirmBody.GetProperty("data").GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task RejectPurchaseRequest_ByNonSeller_ReturnsForbidden()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        using var buyerClient = factory.CreateClient();
        await AuthenticateAsAsync(buyerClient, "other@example.com", UserPassword);

        var create = await buyerClient.PostAsync($"/api/v1/listings/{SeededListingId}/purchase-requests", null);
        create.EnsureSuccessStatusCode();
        var requestId = (await create.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data")
            .GetProperty("id")
            .GetGuid();

        var reject = await buyerClient.PostAsJsonAsync($"/api/v1/purchase-requests/{requestId}/reject", new
        {
            reason = "non-seller reject"
        });

        Assert.Equal(HttpStatusCode.Forbidden, reject.StatusCode);
        var body = await reject.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PURCHASE_REQUEST_ACCESS_DENIED", body.GetProperty("error").GetProperty("code").GetString());
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

    private static async Task<Guid> SeedListingAsync(
        ListingApiFactory factory,
        bool isFree,
        bool isCharity,
        bool isTradeable)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var listingId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Listings.Add(new Listing
        {
            Id = listingId,
            Title = $"交易流程測試商品-{listingId:N}",
            Description = "integration-test",
            Price = isFree ? 0 : 300,
            IsFree = isFree,
            IsCharity = isCharity,
            SellerId = SellerUserId,
            Category = 1,
            PickupLocation = 3,
            Condition = 1,
            BuyerId = null,
            Residence = 2,
            IsTradeable = isTradeable,
            IsPinned = false,
            Status = (int)ListingStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
        return listingId;
    }
}
