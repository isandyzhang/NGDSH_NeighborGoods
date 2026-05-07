using Microsoft.Extensions.DependencyInjection;
using NeighborGoods.Api.Features.Listing;
using NeighborGoods.Api.Features.PurchaseRequests;
using NeighborGoods.Api.Features.Reviews.Contracts;
using NeighborGoods.Api.Features.Reviews.Services;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Tests;

[Collection("ListingApiTests")]
public sealed class ReviewServiceTests(SqlServerContainerFixture fixture)
{
    private const string SellerUserId = "test-user-confirmed";
    private const string BuyerUserId = "test-user-other";
    private const string ThirdUserId = "test-user-unconfirmed";

    /// <summary>
    /// 模擬遷移或歷史資料將評價掛在舊的 PR，而使用者從「正確」的已接受 PR 送評；應視為已評價以避免重複寫入。
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenReviewExistsOnAnotherPurchaseRequestForSameDeal_ReturnsAlreadyExists()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var reviewService = scope.ServiceProvider.GetRequiredService<ReviewService>();

        var listingId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var conversationId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var prBadId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var prGoodId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var now = DateTime.UtcNow;

        var (participant1Id, participant2Id) = string.CompareOrdinal(BuyerUserId, SellerUserId) < 0
            ? (BuyerUserId, SellerUserId)
            : (SellerUserId, BuyerUserId);

        db.Listings.Add(new Listing
        {
            Id = listingId,
            Title = "review-dedupe-test",
            Description = "",
            Price = 100,
            IsFree = false,
            IsCharity = false,
            SellerId = SellerUserId,
            Category = 0,
            PickupLocation = 3,
            Condition = 1,
            BuyerId = null,
            Residence = 2,
            IsTradeable = false,
            IsPinned = false,
            Status = (int)ListingStatus.Sold,
            CreatedAt = now.AddDays(-1),
            UpdatedAt = now
        });

        db.Conversations.Add(new Conversation
        {
            Id = conversationId,
            ListingId = listingId,
            Participant1Id = participant1Id,
            Participant2Id = participant2Id,
            CreatedAt = now,
            UpdatedAt = now
        });

        var cancelled = (int)PurchaseRequestStatus.Cancelled;
        var accepted = (int)PurchaseRequestStatus.Accepted;

        db.PurchaseRequests.Add(new PurchaseRequest
        {
            Id = prBadId,
            ListingId = listingId,
            ConversationId = conversationId,
            BuyerId = BuyerUserId,
            SellerId = SellerUserId,
            Status = cancelled,
            CreatedAt = now.AddHours(-2),
            ExpireAt = now.AddHours(10),
            RespondedAt = now.AddHours(-1)
        });

        db.PurchaseRequests.Add(new PurchaseRequest
        {
            Id = prGoodId,
            ListingId = listingId,
            ConversationId = conversationId,
            BuyerId = BuyerUserId,
            SellerId = SellerUserId,
            Status = accepted,
            CreatedAt = now.AddHours(-1),
            ExpireAt = now.AddHours(11),
            RespondedAt = now
        });

        db.Reviews.Add(new Review
        {
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            ListingId = listingId,
            SellerId = SellerUserId,
            BuyerId = BuyerUserId,
            PurchaseRequestId = prBadId,
            ReviewerId = BuyerUserId,
            Rating = 4,
            Content = "舊 PR 上的評價",
            CreatedAt = now
        });

        await db.SaveChangesAsync();

        var (data, errorCode, _) = await reviewService.CreateAsync(
            BuyerUserId,
            prGoodId,
            new CreateReviewRequest(5, "重複送評"),
            CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("REVIEW_ALREADY_EXISTS", errorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenUserIsNotParticipant_ReturnsAccessDenied()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var reviewService = scope.ServiceProvider.GetRequiredService<ReviewService>();
        var now = DateTime.UtcNow;

        var listingId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        db.Listings.Add(new Listing
        {
            Id = listingId,
            Title = "review-access-test",
            Description = "",
            Price = 100,
            IsFree = false,
            IsCharity = false,
            SellerId = SellerUserId,
            Category = 0,
            PickupLocation = 3,
            Condition = 1,
            BuyerId = null,
            Residence = 2,
            IsTradeable = false,
            IsPinned = false,
            Status = (int)ListingStatus.Sold,
            CreatedAt = now.AddDays(-1),
            UpdatedAt = now
        });
        db.Conversations.Add(new Conversation
        {
            Id = conversationId,
            ListingId = listingId,
            Participant1Id = BuyerUserId,
            Participant2Id = SellerUserId,
            CreatedAt = now.AddHours(-3),
            UpdatedAt = now.AddHours(-2)
        });
        db.PurchaseRequests.Add(new PurchaseRequest
        {
            Id = requestId,
            ListingId = listingId,
            ConversationId = conversationId,
            BuyerId = BuyerUserId,
            SellerId = SellerUserId,
            Status = (int)PurchaseRequestStatus.Completed,
            CreatedAt = now.AddHours(-2),
            ExpireAt = now.AddHours(10),
            RespondedAt = now.AddHours(-1)
        });
        await db.SaveChangesAsync();

        var (data, errorCode, _) = await reviewService.CreateAsync(
            ThirdUserId,
            requestId,
            new CreateReviewRequest(5, "非交易參與者"),
            CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("PURCHASE_REQUEST_ACCESS_DENIED", errorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenPurchaseNotCompleted_ReturnsReviewNotAvailable()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        var reviewService = scope.ServiceProvider.GetRequiredService<ReviewService>();
        var now = DateTime.UtcNow;

        var listingId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        db.Listings.Add(new Listing
        {
            Id = listingId,
            Title = "review-state-test",
            Description = "",
            Price = 100,
            IsFree = false,
            IsCharity = false,
            SellerId = SellerUserId,
            Category = 0,
            PickupLocation = 3,
            Condition = 1,
            BuyerId = null,
            Residence = 2,
            IsTradeable = false,
            IsPinned = false,
            Status = (int)ListingStatus.Reserved,
            CreatedAt = now.AddDays(-1),
            UpdatedAt = now
        });
        db.Conversations.Add(new Conversation
        {
            Id = conversationId,
            ListingId = listingId,
            Participant1Id = BuyerUserId,
            Participant2Id = SellerUserId,
            CreatedAt = now.AddHours(-3),
            UpdatedAt = now.AddHours(-2)
        });
        db.PurchaseRequests.Add(new PurchaseRequest
        {
            Id = requestId,
            ListingId = listingId,
            ConversationId = conversationId,
            BuyerId = BuyerUserId,
            SellerId = SellerUserId,
            Status = (int)PurchaseRequestStatus.Accepted,
            CreatedAt = now.AddHours(-2),
            ExpireAt = now.AddHours(10),
            RespondedAt = now.AddHours(-1)
        });
        await db.SaveChangesAsync();

        var (data, errorCode, errorMessage) = await reviewService.CreateAsync(
            BuyerUserId,
            requestId,
            new CreateReviewRequest(5, "尚未完成交易"),
            CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("REVIEW_NOT_AVAILABLE", errorCode);
        Assert.Contains("僅完成交易後可評價", errorMessage);
    }
}
