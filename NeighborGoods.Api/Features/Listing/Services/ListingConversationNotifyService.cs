using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Features.Listing.Services;

public sealed class ListingConversationNotifyService(
    NeighborGoodsDbContext dbContext,
    ILogger<ListingConversationNotifyService> logger)
{
    /// <summary>與 DB <c>HasMaxLength(50)</c> 相容之固定短文（目前長度 18）。</summary>
    public const string SoldListingSystemMessageContent = "[系統發送]賣家已將商品標示為已售出";

    public async Task<bool> TryNotifyListingSoldAsync(
        Guid listingId,
        string sellerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await NotifyListingSoldAsync(listingId, sellerId, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "寫入已售出系統訊息失敗：ListingId={ListingId}, SellerId={SellerId}",
                listingId,
                sellerId);
            return false;
        }
    }

    private async Task NotifyListingSoldAsync(Guid listingId, string sellerId, CancellationToken cancellationToken)
    {
        var conversations = await dbContext.Conversations
            .Where(c => c.ListingId == listingId)
            .ToListAsync(cancellationToken);

        if (conversations.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var conversation in conversations)
        {
            dbContext.Messages.Add(new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                SenderId = sellerId,
                Content = SoldListingSystemMessageContent,
                CreatedAt = now
            });
            conversation.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
