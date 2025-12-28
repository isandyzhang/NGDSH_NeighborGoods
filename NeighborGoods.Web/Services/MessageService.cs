using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeighborGoods.Web.Data;
using NeighborGoods.Web.Models;
using NeighborGoods.Web.Models.DTOs;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.ViewModels;
using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Services;

/// <summary>
/// 訊息服務實作
/// </summary>
public class MessageService : IMessageService
{
    private readonly AppDbContext _db;
    private readonly ILogger<MessageService> _logger;
    private readonly ILineMessagingApiService? _lineMessagingApiService;
    private readonly INotificationMergeService? _notificationMergeService;

    public MessageService(
        AppDbContext db,
        ILogger<MessageService> logger,
        ILineMessagingApiService? lineMessagingApiService = null,
        INotificationMergeService? notificationMergeService = null)
    {
        _db = db;
        _logger = logger;
        _lineMessagingApiService = lineMessagingApiService;
        _notificationMergeService = notificationMergeService;
    }

    public async Task<int> GetUnreadMessageCountAsync(string userId)
    {
        try
        {
            // 使用單一查詢計算所有對話的未讀數量
            // 分別處理 Participant1 和 Participant2 的情況
            var unreadCount1 = await (
                from c in _db.Conversations
                where c.Participant1Id == userId
                from m in _db.Messages
                where m.ConversationId == c.Id
                    && m.SenderId != userId
                    && (c.Participant1LastReadAt == null || m.CreatedAt > c.Participant1LastReadAt.Value)
                select m
            ).CountAsync();

            var unreadCount2 = await (
                from c in _db.Conversations
                where c.Participant2Id == userId
                from m in _db.Messages
                where m.ConversationId == c.Id
                    && m.SenderId != userId
                    && (c.Participant2LastReadAt == null || m.CreatedAt > c.Participant2LastReadAt.Value)
                select m
            ).CountAsync();

            return unreadCount1 + unreadCount2;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "計算未讀訊息數量時發生錯誤");
            return 0;
        }
    }

    public async Task<ConversationListViewModel> GetConversationsAsync(string userId)
    {
        // 取得當前用戶參與的所有對話
        var conversations = await _db.Conversations
            .Include(c => c.Participant1)
            .Include(c => c.Participant2)
            .Include(c => c.Listing)
            .Where(c => c.Participant1Id == userId || c.Participant2Id == userId)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();

        // 載入所有商品的圖片（批量查詢以提高效能）
        var listingIds = conversations
            .Where(c => c.Listing != null)
            .Select(c => c.Listing!.Id)
            .Distinct()
            .ToList();

        if (listingIds.Any())
        {
            await _db.ListingImages
                .Where(img => listingIds.Contains(img.ListingId))
                .LoadAsync();
        }

        // 取得所有對話 ID
        var conversationIds = conversations.Select(c => c.Id).ToList();

        // 單一查詢取得所有對話的最後一則訊息
        var lastMessages = await _db.Messages
            .Where(m => conversationIds.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => new
            {
                ConversationId = g.Key,
                LastMessage = g.OrderByDescending(m => m.CreatedAt).FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.ConversationId, x => x.LastMessage);

        // 單一查詢計算所有對話的未讀數量
        var unreadCounts1 = await (
            from c in _db.Conversations
            where conversationIds.Contains(c.Id) && c.Participant1Id == userId
            from m in _db.Messages
            where m.ConversationId == c.Id
                && m.SenderId != userId
                && (c.Participant1LastReadAt == null || m.CreatedAt > c.Participant1LastReadAt.Value)
            group m by c.Id into g
            select new
            {
                ConversationId = g.Key,
                UnreadCount = g.Count()
            }
        ).ToDictionaryAsync(x => x.ConversationId, x => x.UnreadCount);

        var unreadCounts2 = await (
            from c in _db.Conversations
            where conversationIds.Contains(c.Id) && c.Participant2Id == userId
            from m in _db.Messages
            where m.ConversationId == c.Id
                && m.SenderId != userId
                && (c.Participant2LastReadAt == null || m.CreatedAt > c.Participant2LastReadAt.Value)
            group m by c.Id into g
            select new
            {
                ConversationId = g.Key,
                UnreadCount = g.Count()
            }
        ).ToDictionaryAsync(x => x.ConversationId, x => x.UnreadCount);

        // 合併兩個字典
        var unreadCountDict = new Dictionary<Guid, int>();
        foreach (var kvp in unreadCounts1)
        {
            unreadCountDict[kvp.Key] = kvp.Value;
        }
        foreach (var kvp in unreadCounts2)
        {
            unreadCountDict[kvp.Key] = kvp.Value;
        }

        var conversationItems = new List<ConversationItemViewModel>();

        foreach (var conversation in conversations)
        {
            // 判斷對方是誰
            var otherUser = conversation.Participant1Id == userId
                ? conversation.Participant2
                : conversation.Participant1;

            if (otherUser == null)
            {
                continue;
            }

            // 取得最後一則訊息
            lastMessages.TryGetValue(conversation.Id, out var lastMessage);

            // 取得未讀數量
            unreadCountDict.TryGetValue(conversation.Id, out var unreadCount);

            // 取得商品的第一張圖片
            string? listingFirstImageUrl = null;
            if (conversation.Listing?.Images != null && conversation.Listing.Images.Any())
            {
                listingFirstImageUrl = conversation.Listing.Images
                    .OrderBy(img => img.SortOrder)
                    .FirstOrDefault()?.ImageUrl;
            }

            conversationItems.Add(new ConversationItemViewModel
            {
                ConversationId = conversation.Id,
                OtherUserId = otherUser.Id,
                OtherUserDisplayName = otherUser.DisplayName,
                LastMessage = lastMessage?.Content,
                LastMessageTime = lastMessage?.CreatedAt,
                UnreadCount = unreadCount,
                ListingId = conversation.ListingId,
                ListingTitle = conversation.Listing?.Title ?? "未知商品",
                ListingFirstImageUrl = listingFirstImageUrl
            });
        }

        return new ConversationListViewModel
        {
            Conversations = conversationItems
        };
    }

    public async Task<ChatViewModel?> GetChatAsync(Guid conversationId, string userId)
    {
        var conversation = await _db.Conversations
            .Include(c => c.Participant1)
            .Include(c => c.Participant2)
            .Include(c => c.Listing)
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
                .ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null)
        {
            return null;
        }

        // 驗證當前用戶是否為對話參與者
        if (conversation.Participant1Id != userId && conversation.Participant2Id != userId)
        {
            return null;
        }

        // 更新當前用戶的最後已讀時間
        var now = TaiwanTime.Now;
        if (conversation.Participant1Id == userId)
        {
            conversation.Participant1LastReadAt = now;
        }
        else
        {
            conversation.Participant2LastReadAt = now;
        }
        await _db.SaveChangesAsync();

        // 判斷對方是誰
        var otherUser = conversation.Participant1Id == userId
            ? conversation.Participant2
            : conversation.Participant1;

        if (otherUser == null)
        {
            return null;
        }

        var messages = conversation.Messages.Select(m => new MessageViewModel
        {
            Id = m.Id,
            SenderId = m.SenderId,
            SenderDisplayName = m.Sender?.DisplayName ?? "未知用戶",
            Content = m.Content,
            CreatedAt = m.CreatedAt,
            IsMine = m.SenderId == userId
        }).ToList();

        var viewModel = new ChatViewModel
        {
            ConversationId = conversation.Id,
            CurrentUserId = userId,
            OtherUserId = otherUser.Id,
            OtherUserDisplayName = otherUser.DisplayName,
            Messages = messages
        };

        // 所有對話都必須關聯商品，從對話中獲取商品資訊
        if (conversation.Listing != null)
        {
            var listing = conversation.Listing;

            // 如果需要在視圖中顯示圖片，需要載入 Images
            if (listing.Images == null || !listing.Images.Any())
            {
                await _db.Entry(listing)
                    .Collection(l => l.Images)
                    .Query()
                    .OrderBy(img => img.SortOrder)
                    .LoadAsync();
            }

            viewModel.ListingId = listing.Id;
            viewModel.ListingTitle = listing.Title;
            viewModel.ListingPrice = listing.Price;
            viewModel.ListingStatus = listing.Status;
            viewModel.ListingIsFree = listing.IsFree;
            viewModel.ListingIsCharity = listing.IsCharity;
            viewModel.ListingSellerId = listing.SellerId;
            viewModel.IsSeller = listing.SellerId == userId;

            // 取得第一張圖片
            var firstImage = listing.Images?.OrderBy(img => img.SortOrder).FirstOrDefault();
            if (firstImage != null)
            {
                viewModel.ListingFirstImageUrl = firstImage.ImageUrl;
            }

            // 檢查當前用戶是否已評價（BuyerId 在 Review 中代表評價者）
            var hasReviewed = await _db.Reviews
                .AnyAsync(r => r.ListingId == listing.Id && r.BuyerId == userId);
            viewModel.HasCurrentUserReviewed = hasReviewed;
        }

        return viewModel;
    }

    public async Task<(Conversation conversation, bool isNew)> GetOrCreateConversationAsync(
        string userId1,
        string userId2,
        Guid listingId)
    {
        // 確保 Participant1Id < Participant2Id，以便統一查詢
        var participant1Id = string.Compare(userId1, userId2, StringComparison.Ordinal) < 0 ? userId1 : userId2;
        var participant2Id = string.Compare(userId1, userId2, StringComparison.Ordinal) < 0 ? userId2 : userId1;

        // 查詢相同用戶和相同商品的對話
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c =>
                c.Participant1Id == participant1Id &&
                c.Participant2Id == participant2Id &&
                c.ListingId == listingId);

        bool isNew = false;
        if (conversation == null)
        {
            isNew = true;
            conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                Participant1Id = participant1Id,
                Participant2Id = participant2Id,
                ListingId = listingId,
                CreatedAt = TaiwanTime.Now,
                UpdatedAt = TaiwanTime.Now
            };

            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync();
        }

        return (conversation, isNew);
    }

    public async Task<ServiceResult<Guid>> SendMessageAsync(
        Guid? conversationId,
        string? receiverId,
        Guid? listingId,
        string content,
        string senderId)
    {
        try
        {
            Conversation? conversation;

            if (conversationId.HasValue)
            {
                // 使用現有對話
                conversation = await _db.Conversations
                    .FirstOrDefaultAsync(c => c.Id == conversationId.Value);

                if (conversation == null)
                {
                    return ServiceResult<Guid>.Fail("找不到對話");
                }

                // 驗證當前用戶是否為對話參與者
                if (conversation.Participant1Id != senderId && conversation.Participant2Id != senderId)
                {
                    return ServiceResult<Guid>.Fail("無權限訪問此對話");
                }
            }
            else if (!string.IsNullOrEmpty(receiverId) && listingId.HasValue)
            {
                // 建立新對話
                if (senderId == receiverId)
                {
                    return ServiceResult<Guid>.Fail("無法與自己對話");
                }

                var (conversationResult, _) = await GetOrCreateConversationAsync(senderId, receiverId, listingId.Value);
                conversation = conversationResult;
            }
            else
            {
                return ServiceResult<Guid>.Fail("必須指定對話或接收者");
            }

            // 建立訊息
            var message = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                SenderId = senderId,
                Content = content.Trim(),
                CreatedAt = TaiwanTime.Now
            };

            _db.Messages.Add(message);

            // 更新對話的最後更新時間
            conversation.UpdatedAt = TaiwanTime.Now;

            await _db.SaveChangesAsync();

            // 整合 LINE 通知（不影響主要功能）
            try
            {
                await SendLineNotificationAsync(conversation, senderId, content, message.CreatedAt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "發送 LINE 通知時發生錯誤，但不影響訊息發送");
            }

            return ServiceResult<Guid>.Ok(message.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送訊息時發生錯誤");
            return ServiceResult<Guid>.Fail("發送訊息時發生錯誤，請稍後再試");
        }
    }

    public async Task<ServiceResult> MarkAsReadAsync(Guid conversationId, string userId)
    {
        try
        {
            var conversation = await _db.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                return ServiceResult.Fail("找不到對話");
            }

            // 驗證當前用戶是否為對話參與者
            if (conversation.Participant1Id != userId && conversation.Participant2Id != userId)
            {
                return ServiceResult.Fail("無權限訪問此對話");
            }

            // 更新當前用戶的最後已讀時間
            var now = TaiwanTime.Now;
            if (conversation.Participant1Id == userId)
            {
                conversation.Participant1LastReadAt = now;
            }
            else
            {
                conversation.Participant2LastReadAt = now;
            }

            await _db.SaveChangesAsync();

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "標記對話為已讀時發生錯誤");
            return ServiceResult.Fail("標記對話為已讀時發生錯誤，請稍後再試");
        }
    }

    /// <summary>
    /// 發送 LINE 通知（私有方法）
    /// </summary>
    private async Task SendLineNotificationAsync(
        Conversation conversation,
        string senderId,
        string messageContent,
        DateTime messageCreatedAt)
    {
        // 如果服務未注入，則不處理
        if (_lineMessagingApiService == null || _notificationMergeService == null)
        {
            return;
        }

        // 判斷接收者是誰
        var receiverId = conversation.Participant1Id == senderId
            ? conversation.Participant2Id
            : conversation.Participant1Id;

        if (string.IsNullOrEmpty(receiverId))
        {
            return;
        }

        // 取得接收者資訊
        var receiver = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == receiverId);

        if (receiver == null || string.IsNullOrEmpty(receiver.LineMessagingApiUserId))
        {
            return;
        }

        // 檢查用戶的通知偏好設定
        // 1=即時, 2=摘要, 3=僅重要, 4=關閉
        if (receiver.LineNotificationPreference == 4)
        {
            return; // 用戶已關閉通知
        }

        // 取得發送者資訊
        var sender = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == senderId);

        var senderName = sender?.DisplayName ?? "未知用戶";

        // 一般訊息使用 Medium 優先級（會合併）
        var priority = Models.Enums.NotificationPriority.Medium;

        // 檢查是否為重要事件（購買請求等）
        // 這裡可以根據訊息內容判斷，或從其他地方傳入
        // 目前先使用 Medium 優先級

        // 根據優先級決定立即通知或加入佇列
        if (priority == Models.Enums.NotificationPriority.High)
        {
            // High 優先級：立即發送
            var notificationMessage = $"{senderName}：{messageContent}";
            var chatUrl = $"/Message/Chat?conversationId={conversation.Id}";
            var fullUrl = $"https://your-site.azurewebsites.net{chatUrl}"; // TODO: 從設定檔取得基礎 URL

            await _lineMessagingApiService.SendPushMessageWithLinkAsync(
                receiver.LineMessagingApiUserId,
                notificationMessage,
                fullUrl,
                "查看對話",
                priority);
        }
        else
        {
            // Medium/Low 優先級：加入合併佇列
            var pendingNotification = new Models.PendingNotification
            {
                UserId = receiverId,
                ConversationId = conversation.Id,
                SenderName = senderName,
                MessageCount = 1,
                LastMessageTime = messageCreatedAt,
                FirstMessageTime = messageCreatedAt,
                Priority = priority,
                MessagePreview = messageContent.Length > 20 ? messageContent.Substring(0, 20) + "..." : messageContent
            };

            _notificationMergeService.AddNotification(receiverId, pendingNotification);
        }
    }

    public async Task<ServiceResult<Models.Entities.Listing>> ValidateListingAsync(Guid listingId)
    {
        try
        {
            var listing = await _db.Listings
                .FirstOrDefaultAsync(l => l.Id == listingId);

            if (listing == null)
            {
                return ServiceResult<Models.Entities.Listing>.Fail("找不到商品");
            }

            return ServiceResult<Models.Entities.Listing>.Ok(listing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驗證商品時發生錯誤：ListingId={ListingId}", listingId);
            return ServiceResult<Models.Entities.Listing>.Fail("驗證商品時發生錯誤，請稍後再試");
        }
    }

    public async Task<ServiceResult<ConversationInfo>> GetConversationForSignalRAsync(
        Guid? conversationId,
        string senderId,
        string? receiverId,
        Guid? listingId)
    {
        try
        {
            Conversation? conversation = null;

            if (conversationId.HasValue)
            {
                conversation = await _db.Conversations
                    .FirstOrDefaultAsync(c => c.Id == conversationId.Value);
            }
            else if (!string.IsNullOrEmpty(receiverId) && listingId.HasValue)
            {
                conversation = await _db.Conversations
                    .FirstOrDefaultAsync(c =>
                        ((c.Participant1Id == senderId && c.Participant2Id == receiverId) ||
                         (c.Participant2Id == senderId && c.Participant1Id == receiverId)) &&
                        c.ListingId == listingId.Value);
            }

            if (conversation == null)
            {
                return ServiceResult<ConversationInfo>.Fail("找不到對話");
            }

            var receiverIdValue = conversation.Participant1Id == senderId
                ? conversation.Participant2Id
                : conversation.Participant1Id;

            return ServiceResult<ConversationInfo>.Ok(new ConversationInfo
            {
                ConversationId = conversation.Id,
                ReceiverId = receiverIdValue
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得對話資訊時發生錯誤");
            return ServiceResult<ConversationInfo>.Fail("取得對話資訊時發生錯誤，請稍後再試");
        }
    }

    public async Task<ServiceResult<Guid>> SendPurchaseRequestAsync(
        Guid conversationId,
        Guid listingId,
        string buyerId,
        bool isFreeOrCharity)
    {
        try
        {
            // 驗證對話
            var conversation = await _db.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                return ServiceResult<Guid>.Fail("找不到對話");
            }

            // 驗證當前用戶是否為對話參與者
            if (conversation.Participant1Id != buyerId && conversation.Participant2Id != buyerId)
            {
                return ServiceResult<Guid>.Fail("無權限訪問此對話");
            }

            // 驗證商品
            var listing = await _db.Listings
                .FirstOrDefaultAsync(l => l.Id == listingId);

            if (listing == null)
            {
                return ServiceResult<Guid>.Fail("找不到商品");
            }

            // 驗證用戶不是賣家
            if (listing.SellerId == buyerId)
            {
                return ServiceResult<Guid>.Fail("無法購買自己的商品");
            }

            // 驗證商品狀態允許購買請求
            if (listing.Status != Models.Enums.ListingStatus.Active)
            {
                return ServiceResult<Guid>.Fail("商品已下架或已售出，無法發送購買請求");
            }

            // 根據商品類型生成訊息內容
            var messageContent = isFreeOrCharity
                ? "[系統發送] 我想索取此商品"
                : "[系統發送] 我想購買此商品";

            // 建立訊息
            var message = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                SenderId = buyerId,
                Content = messageContent,
                CreatedAt = TaiwanTime.Now
            };

            _db.Messages.Add(message);

            // 更新對話的最後更新時間
            conversation.UpdatedAt = TaiwanTime.Now;

            await _db.SaveChangesAsync();

            _logger.LogInformation("發送購買請求：ConversationId={ConversationId}, ListingId={ListingId}, BuyerId={BuyerId}",
                conversationId, listingId, buyerId);

            return ServiceResult<Guid>.Ok(message.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送購買請求時發生錯誤：ConversationId={ConversationId}, ListingId={ListingId}, BuyerId={BuyerId}",
                conversationId, listingId, buyerId);
            return ServiceResult<Guid>.Fail("發送購買請求時發生錯誤，請稍後再試");
        }
    }

    public async Task<ServiceResult<AcceptPurchaseResult>> AcceptPurchaseAsync(
        Guid conversationId,
        Guid listingId,
        string sellerId)
    {
        try
        {
            // 驗證對話
            var conversation = await _db.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                return ServiceResult<AcceptPurchaseResult>.Fail("找不到對話");
            }

            // 驗證當前用戶是否為對話參與者
            if (conversation.Participant1Id != sellerId && conversation.Participant2Id != sellerId)
            {
                return ServiceResult<AcceptPurchaseResult>.Fail("無權限訪問此對話");
            }

            // 驗證商品
            var listing = await _db.Listings
                .FirstOrDefaultAsync(l => l.Id == listingId);

            if (listing == null)
            {
                return ServiceResult<AcceptPurchaseResult>.Fail("找不到商品");
            }

            // 驗證用戶是賣家
            if (listing.SellerId != sellerId)
            {
                return ServiceResult<AcceptPurchaseResult>.Fail("只有賣家可以同意交易");
            }

            // 驗證商品狀態為上架中
            if (listing.Status != Models.Enums.ListingStatus.Active)
            {
                return ServiceResult<AcceptPurchaseResult>.Fail("商品狀態不允許此操作");
            }

            // 更新商品狀態為保留中
            listing.Status = Models.Enums.ListingStatus.Reserved;
            listing.UpdatedAt = TaiwanTime.Now;

            // 發送系統訊息通知買家
            var buyerId = conversation.Participant1Id == sellerId
                ? conversation.Participant2Id
                : conversation.Participant1Id;

            var notificationMessage = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                SenderId = sellerId,
                Content = "[系統發送] 賣家已同意交易，商品狀態已變更為「保留中」。請與賣家確認面交時間和地點。",
                CreatedAt = TaiwanTime.Now
            };

            _db.Messages.Add(notificationMessage);

            // 更新對話的最後更新時間
            conversation.UpdatedAt = TaiwanTime.Now;

            await _db.SaveChangesAsync();

            _logger.LogInformation("賣家同意交易：ConversationId={ConversationId}, ListingId={ListingId}, SellerId={SellerId}",
                conversationId, listingId, sellerId);

            return ServiceResult<AcceptPurchaseResult>.Ok(new AcceptPurchaseResult
            {
                MessageId = notificationMessage.Id,
                BuyerId = buyerId,
                MessageContent = notificationMessage.Content,
                MessageCreatedAt = notificationMessage.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同意交易時發生錯誤：ConversationId={ConversationId}, ListingId={ListingId}, SellerId={SellerId}",
                conversationId, listingId, sellerId);
            return ServiceResult<AcceptPurchaseResult>.Fail("同意交易時發生錯誤，請稍後再試");
        }
    }

    public async Task<ServiceResult<CompleteTransactionResult>> CompleteTransactionAsync(
        Guid conversationId,
        Guid listingId,
        string buyerId)
    {
        try
        {
            // 驗證對話
            var conversation = await _db.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                return ServiceResult<CompleteTransactionResult>.Fail("找不到對話");
            }

            // 驗證當前用戶是否為對話參與者
            if (conversation.Participant1Id != buyerId && conversation.Participant2Id != buyerId)
            {
                return ServiceResult<CompleteTransactionResult>.Fail("無權限訪問此對話");
            }

            // 驗證商品
            var listing = await _db.Listings
                .FirstOrDefaultAsync(l => l.Id == listingId);

            if (listing == null)
            {
                return ServiceResult<CompleteTransactionResult>.Fail("找不到商品");
            }

            // 驗證用戶是買家（不是賣家）
            if (listing.SellerId == buyerId)
            {
                return ServiceResult<CompleteTransactionResult>.Fail("只有買家可以完成交易");
            }

            // 驗證商品狀態為保留中
            if (listing.Status != Models.Enums.ListingStatus.Reserved)
            {
                return ServiceResult<CompleteTransactionResult>.Fail("商品狀態不允許此操作");
            }

            // 更新商品狀態為已售出
            listing.Status = Models.Enums.ListingStatus.Sold;
            listing.UpdatedAt = TaiwanTime.Now;

            // 發送系統訊息通知賣家
            var sellerId = listing.SellerId;

            var notificationMessage = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                SenderId = buyerId,
                Content = "[系統發送] 買家已完成交易，商品狀態已變更為「已售出」。請對買家進行評價。",
                CreatedAt = TaiwanTime.Now
            };

            _db.Messages.Add(notificationMessage);

            // 更新對話的最後更新時間
            conversation.UpdatedAt = TaiwanTime.Now;

            await _db.SaveChangesAsync();

            _logger.LogInformation("買家完成交易：ConversationId={ConversationId}, ListingId={ListingId}, BuyerId={BuyerId}",
                conversationId, listingId, buyerId);

            return ServiceResult<CompleteTransactionResult>.Ok(new CompleteTransactionResult
            {
                MessageId = notificationMessage.Id,
                SellerId = sellerId,
                MessageContent = notificationMessage.Content,
                MessageCreatedAt = notificationMessage.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "完成交易時發生錯誤：ConversationId={ConversationId}, ListingId={ListingId}, BuyerId={BuyerId}",
                conversationId, listingId, buyerId);
            return ServiceResult<CompleteTransactionResult>.Fail("完成交易時發生錯誤，請稍後再試");
        }
    }

    public async Task<ServiceResult<Models.ViewModels.ReviewViewModel>> GetReviewInfoAsync(
        Guid listingId,
        Guid conversationId,
        string currentUserId)
    {
        try
        {
            // 驗證對話
            var conversation = await _db.Conversations
                .Include(c => c.Participant1)
                .Include(c => c.Participant2)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                return ServiceResult<Models.ViewModels.ReviewViewModel>.Fail("找不到對話");
            }

            // 驗證當前用戶是否為對話參與者
            if (conversation.Participant1Id != currentUserId && conversation.Participant2Id != currentUserId)
            {
                return ServiceResult<Models.ViewModels.ReviewViewModel>.Fail("無權限訪問此對話");
            }

            // 驗證商品
            var listing = await _db.Listings
                .FirstOrDefaultAsync(l => l.Id == listingId);

            if (listing == null)
            {
                return ServiceResult<Models.ViewModels.ReviewViewModel>.Fail("找不到商品");
            }

            // 判斷對方是誰
            var otherUser = conversation.Participant1Id == currentUserId
                ? conversation.Participant2
                : conversation.Participant1;

            if (otherUser == null)
            {
                return ServiceResult<Models.ViewModels.ReviewViewModel>.Fail("找不到對方用戶");
            }

            // 判斷當前用戶是買家還是賣家
            var isBuyer = listing.SellerId != currentUserId;
            var isBuyerReviewingSeller = isBuyer;

            var viewModel = new Models.ViewModels.ReviewViewModel
            {
                ListingId = listing.Id,
                ConversationId = conversation.Id,
                ListingTitle = listing.Title,
                OtherUserId = otherUser.Id,
                OtherUserDisplayName = otherUser.DisplayName,
                IsBuyerReviewingSeller = isBuyerReviewingSeller
            };

            return ServiceResult<Models.ViewModels.ReviewViewModel>.Ok(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得評價資訊時發生錯誤：ListingId={ListingId}, ConversationId={ConversationId}, UserId={UserId}",
                listingId, conversationId, currentUserId);
            return ServiceResult<Models.ViewModels.ReviewViewModel>.Fail("取得評價資訊時發生錯誤，請稍後再試");
        }
    }
}

