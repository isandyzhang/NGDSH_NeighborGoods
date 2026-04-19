using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Messaging.Contracts.Responses;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Features.Messaging.Services;

public sealed class MessagingCommandService(
    NeighborGoodsDbContext dbContext,
    IHubContext<NeighborGoods.Api.Features.Messaging.MessageHub> hubContext)
{
    public async Task<(Guid? ConversationId, string? ErrorCode, string? ErrorMessage)> EnsureConversationAsync(
        string currentUserId,
        Guid listingId,
        string otherUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(otherUserId))
        {
            return (null, "MESSAGE_VALIDATION_FAILED", "必須指定對方使用者。");
        }

        if (string.Equals(currentUserId, otherUserId, StringComparison.Ordinal))
        {
            return (null, "SELF_CONVERSATION_NOT_ALLOWED", "無法與自己建立對話。");
        }

        var listing = await dbContext.Listings
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == listingId, cancellationToken);

        if (listing is null)
        {
            return (null, "LISTING_NOT_FOUND", "找不到商品");
        }

        var sellerInPair = string.Equals(listing.SellerId, currentUserId, StringComparison.Ordinal)
            || string.Equals(listing.SellerId, otherUserId, StringComparison.Ordinal);
        if (!sellerInPair)
        {
            return (null, "INVALID_CONVERSATION_PARTICIPANTS", "此對話必須包含該商品的賣家。");
        }

        var otherExists = await dbContext.AspNetUsers
            .AnyAsync(u => u.Id == otherUserId, cancellationToken);
        if (!otherExists)
        {
            return (null, "INVALID_CONVERSATION_PARTICIPANTS", "找不到對方使用者。");
        }

        var participant1Id = string.CompareOrdinal(currentUserId, otherUserId) < 0
            ? currentUserId
            : otherUserId;
        var participant2Id = string.CompareOrdinal(currentUserId, otherUserId) < 0
            ? otherUserId
            : currentUserId;

        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(
                c =>
                    c.Participant1Id == participant1Id &&
                    c.Participant2Id == participant2Id &&
                    c.ListingId == listingId,
                cancellationToken);

        if (conversation is not null)
        {
            return (conversation.Id, null, null);
        }

        var now = DateTime.UtcNow;
        conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Participant1Id = participant1Id,
            Participant2Id = participant2Id,
            ListingId = listingId,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (conversation.Id, null, null);
    }

    public async Task<(MessageItemDto? Message, string? ErrorCode, string? ErrorMessage)> SendMessageAsync(
        string currentUserId,
        Guid conversationId,
        string content,
        CancellationToken cancellationToken = default)
    {
        var trimmed = content.Trim();
        if (trimmed.Length == 0)
        {
            return (null, "MESSAGE_VALIDATION_FAILED", "訊息內容不可為空白。");
        }

        if (trimmed.Length > MessagingConstants.MaxMessageContentLength)
        {
            return (null, "MESSAGE_VALIDATION_FAILED", $"訊息長度不可超過 {MessagingConstants.MaxMessageContentLength} 字元。");
        }

        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        if (conversation is null)
        {
            return (null, "CONVERSATION_NOT_FOUND", "找不到對話");
        }

        if (conversation.Participant1Id != currentUserId && conversation.Participant2Id != currentUserId)
        {
            return (null, "CONVERSATION_ACCESS_DENIED", "無權限訪問此對話");
        }

        var now = DateTime.UtcNow;
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderId = currentUserId,
            Content = trimmed,
            CreatedAt = now
        };

        dbContext.Messages.Add(message);
        conversation.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        var sender = await dbContext.AspNetUsers
            .AsNoTracking()
            .FirstAsync(u => u.Id == currentUserId, cancellationToken);

        var dto = new MessageItemDto
        {
            Id = message.Id,
            SenderId = message.SenderId,
            SenderDisplayName = sender.DisplayName,
            Content = message.Content,
            CreatedAt = message.CreatedAt
        };

        var participantIds = new[] { conversation.Participant1Id, conversation.Participant2Id };
        await hubContext.Clients.Users(participantIds).SendAsync(
            "ReceiveMessage",
            dto.SenderId,
            dto.SenderDisplayName,
            dto.Content,
            dto.CreatedAt,
            cancellationToken);

        return (dto, null, null);
    }

    public async Task<(bool Ok, string? ErrorCode, string? ErrorMessage)> MarkReadAsync(
        string currentUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        if (conversation is null)
        {
            return (false, "CONVERSATION_NOT_FOUND", "找不到對話");
        }

        if (conversation.Participant1Id != currentUserId && conversation.Participant2Id != currentUserId)
        {
            return (false, "CONVERSATION_ACCESS_DENIED", "無權限訪問此對話");
        }

        var now = DateTime.UtcNow;
        if (string.Equals(conversation.Participant1Id, currentUserId, StringComparison.Ordinal))
        {
            conversation.Participant1LastReadAt = now;
        }
        else
        {
            conversation.Participant2LastReadAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, null, null);
    }
}
