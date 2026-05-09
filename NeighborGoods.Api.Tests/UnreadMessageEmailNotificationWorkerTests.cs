using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NeighborGoods.Api.Features.Messaging.Services;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Tests;

[Collection("ListingApiTests")]
public sealed class UnreadMessageEmailNotificationWorkerTests(SqlServerContainerFixture fixture)
{
    private static readonly Guid SeededListingId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string SellerUserId = "test-user-confirmed";
    private const string BuyerUserId = "test-user-other";
    private const string BuyerEmail = "other@example.com";

    [Fact]
    public async Task ProcessOnce_SendsOneUnreadNotificationAndThrottlesRepeats()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);
        var conversationId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();

            dbContext.Conversations.Add(new Conversation
            {
                Id = conversationId,
                Participant1Id = SellerUserId,
                Participant2Id = BuyerUserId,
                ListingId = SeededListingId,
                CreatedAt = now.AddMinutes(-30),
                UpdatedAt = now.AddMinutes(-30)
            });

            dbContext.Messages.AddRange(Enumerable.Range(0, 25).Select(index => new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                SenderId = SellerUserId,
                Content = $"unread-{index}",
                CreatedAt = now.AddMinutes(-20).AddSeconds(index)
            }));

            await dbContext.SaveChangesAsync();
        }

        FakeEmailSender.Reset();
        var worker = factory.Services
            .GetServices<IHostedService>()
            .OfType<UnreadMessageEmailNotificationWorker>()
            .Single();

        await worker.ProcessOnceAsync(CancellationToken.None);

        var sentEmails = FakeEmailSender.GetSentEmails()
            .Where(x => x.Subject == "你有未讀訊息")
            .ToList();
        var sentEmail = Assert.Single(sentEmails);
        Assert.Equal(BuyerEmail, sentEmail.ToEmail);
        Assert.Contains($"/messages/{conversationId}", sentEmail.PlainTextContent);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var buyer = await dbContext.AspNetUsers
                .AsNoTracking()
                .SingleAsync(x => x.Id == BuyerUserId);
            Assert.NotNull(buyer.EmailNotificationLastSentAt);
        }

        FakeEmailSender.Reset();
        await worker.ProcessOnceAsync(CancellationToken.None);

        Assert.DoesNotContain(
            FakeEmailSender.GetSentEmails(),
            x => x.Subject == "你有未讀訊息");
    }
}
