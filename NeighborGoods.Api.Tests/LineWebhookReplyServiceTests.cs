using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeighborGoods.Notifications;
using NeighborGoods.Workers.Line;

namespace NeighborGoods.Api.Tests;

public sealed class LineWebhookReplyServiceTests
{
    private const string ChannelSecret = "test-line-channel-secret";

    public LineWebhookReplyServiceTests()
    {
        FakeLineMessageSender.Reset();
    }

    [Fact]
    public async Task ProcessAsync_InvalidSignature_ReturnsUnauthorizedCode()
    {
        var service = CreateService(new FakeLineMenuQueryService());

        var (ok, errorCode, _) = await service.ProcessAsync(
            """{"events":[]}""",
            "invalid-signature");

        Assert.False(ok);
        Assert.Equal("LINE_WEBHOOK_SIGNATURE_INVALID", errorCode);
        Assert.Empty(FakeLineMessageSender.ReplyFlexMessages);
    }

    [Fact]
    public async Task ProcessAsync_MyListings_WhenUnbound_RepliesBindHintCard()
    {
        var service = CreateService(new FakeLineMenuQueryService { BoundUser = null });
        var body = BuildPostbackBody("action=myListings", "U-unbound", "reply-token-1");
        var signature = Sign(body);

        var (ok, errorCode, _) = await service.ProcessAsync(body, signature);

        Assert.True(ok);
        Assert.Null(errorCode);
        Assert.Single(FakeLineMessageSender.ReplyFlexMessages);
        Assert.Equal("reply-token-1", FakeLineMessageSender.ReplyFlexMessages[0].ReplyToken);
        Assert.Contains("綁定", FakeLineMessageSender.ReplyFlexMessages[0].AltText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_MyListings_WhenBound_RepliesListingsCard()
    {
        var query = new FakeLineMenuQueryService
        {
            BoundUser = new LineBoundUser("user-1", "測試用戶"),
            ListingsSummary = new LineMyListingsSummary(1, 1, 0, 0),
            ListingItems =
            [
                new LineMyListingCardItem(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    "測試商品",
                    "說明",
                    "https://example.com/cover.jpg",
                    Status: 0,
                    ResidenceName: "社區A",
                    PickupLocationName: "公設",
                    IsFree: false,
                    Price: 100,
                    FavoriteCount: 2,
                    LastStatusChangedAt: DateTime.UtcNow,
                    UnreadCount: 0,
                    UpdatedAt: DateTime.UtcNow,
                    CreatedAt: DateTime.UtcNow)
            ]
        };
        var service = CreateService(query);
        var body = BuildPostbackBody("action=myListings", "U-bound", "reply-token-listings");
        var signature = Sign(body);

        var (ok, errorCode, _) = await service.ProcessAsync(body, signature);

        Assert.True(ok);
        Assert.Null(errorCode);
        Assert.Single(FakeLineMessageSender.ReplyFlexMessages);
        Assert.Equal("reply-token-listings", FakeLineMessageSender.ReplyFlexMessages[0].ReplyToken);
        Assert.Equal(1, query.MyListingsSummaryCalls);
        Assert.Equal(1, query.MyListingCardItemsCalls);
    }

    [Fact]
    public async Task ProcessAsync_MyMessages_WhenBound_RepliesMessagesCard()
    {
        var conversationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var query = new FakeLineMenuQueryService
        {
            BoundUser = new LineBoundUser("user-2", "訊息用戶"),
            MessagesSummary = new LineMyMessagesSummary(
                1,
                3,
                "訊息用戶",
                DateTime.UtcNow.AddDays(-10),
                [
                    new LineRecentConversationItem(
                        conversationId,
                        "對方",
                        "商品B",
                        "你好",
                        DateTime.UtcNow,
                        3)
                ])
        };
        var service = CreateService(query);
        var body = BuildPostbackBody("action=myMessages", "U-bound-msg", "reply-token-messages");
        var signature = Sign(body);

        var (ok, errorCode, _) = await service.ProcessAsync(body, signature);

        Assert.True(ok);
        Assert.Null(errorCode);
        Assert.Single(FakeLineMessageSender.ReplyFlexMessages);
        Assert.Equal("reply-token-messages", FakeLineMessageSender.ReplyFlexMessages[0].ReplyToken);
        Assert.Contains("我的訊息", FakeLineMessageSender.ReplyFlexMessages[0].AltText, StringComparison.Ordinal);
        Assert.Equal(1, query.MyMessagesSummaryCalls);
    }

    private static LineWebhookReplyService CreateService(ILineMenuQueryService menuQuery)
    {
        var options = Options.Create(new LineMessagingOptions
        {
            ChannelSecret = ChannelSecret,
            ChannelAccessToken = "test-token",
            WebBaseUrl = "https://www.example.com",
            LiffId = "liff-test"
        });

        return new LineWebhookReplyService(
            menuQuery,
            new LineFlexMessageBuilder(options),
            new FakeLineMessageSender(),
            new MemoryCache(new MemoryCacheOptions()),
            options,
            NullLogger<LineWebhookReplyService>.Instance);
    }

    private static string BuildPostbackBody(string postbackData, string userId, string replyToken)
    {
        return $$"""
            {
              "events": [
                {
                  "type": "postback",
                  "replyToken": "{{replyToken}}",
                  "source": {
                    "type": "user",
                    "userId": "{{userId}}"
                  },
                  "postback": {
                    "data": "{{postbackData}}"
                  }
                }
              ]
            }
            """;
    }

    private static string Sign(string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(ChannelSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToBase64String(hash);
    }

    private sealed class FakeLineMenuQueryService : ILineMenuQueryService
    {
        public LineBoundUser? BoundUser { get; set; }
        public LineMyListingsSummary ListingsSummary { get; set; } = new(0, 0, 0, 0);
        public IReadOnlyList<LineMyListingCardItem> ListingItems { get; set; } = [];
        public LineMyMessagesSummary MessagesSummary { get; set; } =
            new(0, 0, "guest", null, Array.Empty<LineRecentConversationItem>());

        public int MyListingsSummaryCalls { get; private set; }
        public int MyListingCardItemsCalls { get; private set; }
        public int MyMessagesSummaryCalls { get; private set; }

        public Task<LineBoundUser?> GetBoundUserAsync(string lineUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(BoundUser);

        public Task<LineMyListingsSummary> GetMyListingsSummaryAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            MyListingsSummaryCalls++;
            return Task.FromResult(ListingsSummary);
        }

        public Task<IReadOnlyList<LineMyListingCardItem>> GetMyListingCardItemsAsync(
            string userId,
            int maxItems = 5,
            CancellationToken cancellationToken = default)
        {
            MyListingCardItemsCalls++;
            return Task.FromResult(ListingItems);
        }

        public Task<LineMyMessagesSummary> GetMyMessagesSummaryAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            MyMessagesSummaryCalls++;
            return Task.FromResult(MessagesSummary);
        }
    }
}
