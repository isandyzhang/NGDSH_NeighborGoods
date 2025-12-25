using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Web.Data;
using NeighborGoods.Web.Hubs;
using NeighborGoods.Web.Infrastructure;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.ViewModels;
using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Controllers;

[Authorize]
public class MessageController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IHubContext<MessageHub> _hubContext;
    private readonly ILogger<MessageController> _logger;

    public MessageController(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IHubContext<MessageHub> hubContext,
        ILogger<MessageController> logger)
        : base(userManager)
    {
        _db = db;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// 顯示對話列表
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Conversations()
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Challenge();
        }

        // 取得當前用戶參與的所有對話
        var conversations = await _db.Conversations
            .Include(c => c.Participant1)
            .Include(c => c.Participant2)
            .Include(c => c.Listing)
            .Where(c => c.Participant1Id == currentUser.Id || c.Participant2Id == currentUser.Id)
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
        // 分別處理 Participant1 和 Participant2 的情況以提高查詢效率
        var unreadCounts1 = await (
            from c in _db.Conversations
            where conversationIds.Contains(c.Id) && c.Participant1Id == currentUser.Id
            from m in _db.Messages
            where m.ConversationId == c.Id
                && m.SenderId != currentUser.Id
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
            where conversationIds.Contains(c.Id) && c.Participant2Id == currentUser.Id
            from m in _db.Messages
            where m.ConversationId == c.Id
                && m.SenderId != currentUser.Id
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
            var otherUser = conversation.Participant1Id == currentUser.Id
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

        var viewModel = new ConversationListViewModel
        {
            Conversations = conversationItems
        };

        return View(viewModel);
    }

    /// <summary>
    /// 顯示特定對話的訊息
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Chat(Guid conversationId, Guid? listingId = null)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Challenge();
        }

        var conversation = await _db.Conversations
            .Include(c => c.Participant1)
            .Include(c => c.Participant2)
            .Include(c => c.Listing)
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
                .ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null)
        {
            return NotFound();
        }

        // 驗證當前用戶是否為對話參與者
        if (conversation.Participant1Id != currentUser.Id && conversation.Participant2Id != currentUser.Id)
        {
            return Forbid();
        }

        // 更新當前用戶的最後已讀時間
        var now = TaiwanTime.Now;
        if (conversation.Participant1Id == currentUser.Id)
        {
            conversation.Participant1LastReadAt = now;
        }
        else
        {
            conversation.Participant2LastReadAt = now;
        }
        await _db.SaveChangesAsync();

        // 判斷對方是誰
        var otherUser = conversation.Participant1Id == currentUser.Id
            ? conversation.Participant2
            : conversation.Participant1;

        if (otherUser == null)
        {
            return NotFound();
        }

        var messages = conversation.Messages.Select(m => new MessageViewModel
        {
            Id = m.Id,
            SenderId = m.SenderId,
            SenderDisplayName = m.Sender?.DisplayName ?? "未知用戶",
            Content = m.Content,
            CreatedAt = m.CreatedAt,
            IsMine = m.SenderId == currentUser.Id
        }).ToList();

        var viewModel = new ChatViewModel
        {
            ConversationId = conversation.Id,
            CurrentUserId = currentUser.Id,
            OtherUserId = otherUser.Id,
            OtherUserDisplayName = otherUser.DisplayName,
            Messages = messages
        };

        // 所有對話都必須關聯商品，從對話中獲取商品資訊
        if (conversation.Listing != null)
        {
            // 已經在 Include 中載入了 Listing，不需要再查詢
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
            viewModel.IsSeller = listing.SellerId == currentUser.Id;
            
            // 取得第一張圖片
            var firstImage = listing.Images?.OrderBy(img => img.SortOrder).FirstOrDefault();
            if (firstImage != null)
            {
                viewModel.ListingFirstImageUrl = firstImage.ImageUrl;
            }
            
            // 檢查當前用戶是否已評價（BuyerId 在 Review 中代表評價者）
            var hasReviewed = await _db.Reviews
                .AnyAsync(r => r.ListingId == listing.Id && r.BuyerId == currentUser.Id);
            viewModel.HasCurrentUserReviewed = hasReviewed;
        }

        return View(viewModel);
    }

    /// <summary>
    /// 與特定用戶開始對話（如果對話不存在則建立）
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ChatWithUser(string userId, Guid listingId, bool sendInitialMessage = true)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Challenge();
        }

        if (currentUser.Id == userId)
        {
            return BadRequest("無法與自己對話");
        }

        var otherUser = await UserManager.FindByIdAsync(userId);
        if (otherUser == null)
        {
            return NotFound("找不到該用戶");
        }

        // 驗證商品是否存在
        var listing = await _db.Listings.FirstOrDefaultAsync(l => l.Id == listingId);
        if (listing == null)
        {
            return NotFound("找不到該商品");
        }

        // 取得或建立對話（所有對話都必須關聯商品）
        var (conversation, isNew) = await GetOrCreateConversationAsync(currentUser.Id, userId, listingId);

        // 如果是新建立的對話且需要發送初始訊息，自動發送系統訊息
        if (isNew && sendInitialMessage)
        {
            var systemMessage = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                SenderId = currentUser.Id,
                Content = "[系統發送] 請問商品還有嗎？",
                CreatedAt = TaiwanTime.Now
            };

            _db.Messages.Add(systemMessage);
            conversation.UpdatedAt = TaiwanTime.Now;
            await _db.SaveChangesAsync();

            // 透過 SignalR 推送訊息給雙方參與者
            await _hubContext.Clients.Users(new[] { currentUser.Id, userId }).SendAsync(
                "ReceiveMessage",
                currentUser.Id,
                currentUser.DisplayName,
                systemMessage.Content,
                systemMessage.CreatedAt);
        }

        return RedirectToAction(nameof(Chat), new { conversationId = conversation.Id, listingId = listingId });
    }

    /// <summary>
    /// 發送訊息
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("SendMessage")]
    public async Task<IActionResult> SendMessage(SendMessageViewModel model)
    {
        var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            if (isAjax)
            {
                return JsonError("未登入");
            }
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            if (isAjax)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return JsonError(string.Join(", ", errors));
            }
            
            // 如果有 ConversationId，返回聊天頁面；否則返回對話列表
            if (model.ConversationId.HasValue)
            {
                return RedirectToAction(nameof(Chat), new { conversationId = model.ConversationId.Value });
            }
            return RedirectToAction(nameof(Conversations));
        }

        Conversation? conversation;

        if (model.ConversationId.HasValue)
        {
            // 使用現有對話
            conversation = await _db.Conversations
                .FirstOrDefaultAsync(c => c.Id == model.ConversationId.Value);

            if (conversation == null)
            {
                if (isAjax)
                {
                    return JsonError("找不到對話");
                }
                return NotFound("找不到對話");
            }

            // 驗證當前用戶是否為對話參與者
            if (conversation.Participant1Id != currentUser.Id && conversation.Participant2Id != currentUser.Id)
            {
                if (isAjax)
                {
                    return JsonError("無權限訪問此對話");
                }
                return Forbid();
            }
        }
        else if (!string.IsNullOrEmpty(model.ReceiverId))
        {
            // 建立新對話（所有對話都必須關聯商品）
            if (!model.ListingId.HasValue)
            {
                if (isAjax)
                {
                    return JsonError("必須指定商品 ID");
                }
                return BadRequest("必須指定商品 ID");
            }

            if (currentUser.Id == model.ReceiverId)
            {
                if (isAjax)
                {
                    return JsonError("無法與自己對話");
                }
                return BadRequest("無法與自己對話");
            }

            var receiver = await UserManager.FindByIdAsync(model.ReceiverId);
            if (receiver == null)
            {
                if (isAjax)
                {
                    return JsonError("找不到接收者");
                }
                return NotFound("找不到接收者");
            }

            // 驗證商品是否存在
            var listing = await _db.Listings.FirstOrDefaultAsync(l => l.Id == model.ListingId.Value);
            if (listing == null)
            {
                if (isAjax)
                {
                    return JsonError("找不到商品");
                }
                return NotFound("找不到商品");
            }

            var (conversationResult, _) = await GetOrCreateConversationAsync(currentUser.Id, model.ReceiverId, model.ListingId.Value);
            conversation = conversationResult;
        }
        else
        {
            if (isAjax)
            {
                return JsonError("必須指定對話或接收者");
            }
            return BadRequest("必須指定對話或接收者");
        }

        // 建立訊息
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderId = currentUser.Id,
            Content = model.Content.Trim(),
            CreatedAt = TaiwanTime.Now
        };

        _db.Messages.Add(message);

        // 更新對話的最後更新時間
        conversation.UpdatedAt = TaiwanTime.Now;

        await _db.SaveChangesAsync();

        // 透過 SignalR 推送訊息給接收者
        var receiverId = conversation.Participant1Id == currentUser.Id
            ? conversation.Participant2Id
            : conversation.Participant1Id;

        await _hubContext.Clients.Users(new[] { currentUser.Id, receiverId }).SendAsync(
            "ReceiveMessage",
            currentUser.Id,
            currentUser.DisplayName,
            message.Content,
            message.CreatedAt);

        // 如果是 AJAX 請求，返回 JSON；否則重定向
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { success = true, messageId = message.Id });
        }

        return RedirectToAction(nameof(Chat), new { conversationId = conversation.Id });
    }

    /// <summary>
    /// 發送購買/索取請求
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendPurchaseRequest([FromBody] SendPurchaseRequestViewModel model)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return JsonError("未登入");
        }

        // 驗證對話
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == model.ConversationId);

        if (conversation == null)
        {
            return Json(new { success = false, error = "找不到對話" });
        }

        // 驗證當前用戶是否為對話參與者
        if (conversation.Participant1Id != currentUser.Id && conversation.Participant2Id != currentUser.Id)
        {
            return Json(new { success = false, error = "無權限訪問此對話" });
        }

        // 驗證商品
        var listing = await _db.Listings
            .FirstOrDefaultAsync(l => l.Id == model.ListingId);

        if (listing == null)
        {
            return Json(new { success = false, error = "找不到商品" });
        }

        // 驗證用戶不是賣家
        if (listing.SellerId == currentUser.Id)
        {
            return JsonError("無法購買自己的商品");
        }

        // 驗證商品狀態允許購買請求
        if (listing.Status != Models.Enums.ListingStatus.Active)
        {
            return JsonError("商品已下架或已售出，無法發送購買請求");
        }

        // 根據商品類型生成訊息內容
        var messageContent = model.IsFreeOrCharity
            ? "[系統發送] 我想索取此商品"
            : "[系統發送] 我想購買此商品";

        // 建立訊息
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderId = currentUser.Id,
            Content = messageContent,
            CreatedAt = TaiwanTime.Now
        };

        _db.Messages.Add(message);

        // 更新對話的最後更新時間
        conversation.UpdatedAt = TaiwanTime.Now;

        await _db.SaveChangesAsync();

        // 透過 SignalR 推送訊息給雙方參與者（包括發送者自己）
        var receiverId = conversation.Participant1Id == currentUser.Id
            ? conversation.Participant2Id
            : conversation.Participant1Id;

        await _hubContext.Clients.Users(new[] { currentUser.Id, receiverId }).SendAsync(
            "ReceiveMessage",
            currentUser.Id,
            currentUser.DisplayName,
            message.Content,
            message.CreatedAt);

        // 通知賣家刷新頁面以顯示「同意交易」按鈕
        await _hubContext.Clients.User(receiverId).SendAsync("RefreshChat", conversation.Id);

        return Json(new { success = true, messageId = message.Id });
    }

    /// <summary>
    /// 賣家同意交易
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptPurchase([FromBody] SendPurchaseRequestViewModel model)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return JsonError("未登入");
        }

        // 驗證對話
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == model.ConversationId);

        if (conversation == null)
        {
            return Json(new { success = false, error = "找不到對話" });
        }

        // 驗證當前用戶是否為對話參與者
        if (conversation.Participant1Id != currentUser.Id && conversation.Participant2Id != currentUser.Id)
        {
            return Json(new { success = false, error = "無權限訪問此對話" });
        }

        // 驗證商品
        var listing = await _db.Listings
            .FirstOrDefaultAsync(l => l.Id == model.ListingId);

        if (listing == null)
        {
            return Json(new { success = false, error = "找不到商品" });
        }

        // 驗證用戶是賣家
        if (listing.SellerId != currentUser.Id)
        {
            return JsonError("只有賣家可以同意交易");
        }

        // 驗證商品狀態為上架中
        if (listing.Status != Models.Enums.ListingStatus.Active)
        {
            return JsonError("商品狀態不允許此操作");
        }

        // 更新商品狀態為保留中
        listing.Status = Models.Enums.ListingStatus.Reserved;
        listing.UpdatedAt = TaiwanTime.Now;

        // 發送系統訊息通知買家
        var buyerId = conversation.Participant1Id == currentUser.Id
            ? conversation.Participant2Id
            : conversation.Participant1Id;

        var notificationMessage = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderId = currentUser.Id,
            Content = "[系統發送] 賣家已同意交易，商品狀態已變更為「保留中」。請與賣家確認面交時間和地點。",
            CreatedAt = TaiwanTime.Now
        };

        _db.Messages.Add(notificationMessage);

        // 更新對話的最後更新時間
        conversation.UpdatedAt = TaiwanTime.Now;

        await _db.SaveChangesAsync();

        // 透過 SignalR 推送訊息給雙方參與者
        await _hubContext.Clients.Users(new[] { currentUser.Id, buyerId }).SendAsync(
            "ReceiveMessage",
            currentUser.Id,
            currentUser.DisplayName,
            notificationMessage.Content,
            notificationMessage.CreatedAt);

        // 通知買家刷新頁面以顯示「完成交易」按鈕
        await _hubContext.Clients.User(buyerId).SendAsync("RefreshChat", conversation.Id);

        return Json(new { success = true });
    }

    /// <summary>
    /// 買家完成交易
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteTransaction([FromBody] SendPurchaseRequestViewModel model)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return JsonError("未登入");
        }

        // 驗證對話
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == model.ConversationId);

        if (conversation == null)
        {
            return Json(new { success = false, error = "找不到對話" });
        }

        // 驗證當前用戶是否為對話參與者
        if (conversation.Participant1Id != currentUser.Id && conversation.Participant2Id != currentUser.Id)
        {
            return Json(new { success = false, error = "無權限訪問此對話" });
        }

        // 驗證商品
        var listing = await _db.Listings
            .FirstOrDefaultAsync(l => l.Id == model.ListingId);

        if (listing == null)
        {
            return Json(new { success = false, error = "找不到商品" });
        }

        // 驗證用戶是買家（不是賣家）
        if (listing.SellerId == currentUser.Id)
        {
            return JsonError("只有買家可以完成交易");
        }

        // 驗證商品狀態為保留中
        if (listing.Status != Models.Enums.ListingStatus.Reserved)
        {
            return JsonError("商品狀態不允許此操作");
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
            SenderId = currentUser.Id,
            Content = "[系統發送] 買家已完成交易，商品狀態已變更為「已售出」。請對買家進行評價。",
            CreatedAt = TaiwanTime.Now
        };

        _db.Messages.Add(notificationMessage);

        // 更新對話的最後更新時間
        conversation.UpdatedAt = TaiwanTime.Now;

        await _db.SaveChangesAsync();

        // 透過 SignalR 推送訊息給雙方參與者
        await _hubContext.Clients.Users(new[] { currentUser.Id, sellerId }).SendAsync(
            "ReceiveMessage",
            currentUser.Id,
            currentUser.DisplayName,
            notificationMessage.Content,
            notificationMessage.CreatedAt);

        // 通知賣家刷新頁面以更新商品狀態
        await _hubContext.Clients.User(sellerId).SendAsync("RefreshChat", conversation.Id);

        return Json(new { success = true });
    }

    /// <summary>
    /// 顯示評價頁面
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Review(Guid listingId, Guid conversationId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Challenge();
        }

        // 驗證對話
        var conversation = await _db.Conversations
            .Include(c => c.Participant1)
            .Include(c => c.Participant2)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null)
        {
            return NotFound();
        }

        // 驗證當前用戶是否為對話參與者
        if (conversation.Participant1Id != currentUser.Id && conversation.Participant2Id != currentUser.Id)
        {
            return Forbid();
        }

        // 驗證商品
        var listing = await _db.Listings
            .FirstOrDefaultAsync(l => l.Id == listingId);

        if (listing == null)
        {
            return NotFound();
        }

        // 判斷對方是誰
        var otherUser = conversation.Participant1Id == currentUser.Id
            ? conversation.Participant2
            : conversation.Participant1;

        if (otherUser == null)
        {
            return NotFound();
        }

        // 判斷當前用戶是買家還是賣家
        var isBuyer = listing.SellerId != currentUser.Id;
        var isBuyerReviewingSeller = isBuyer;

        var viewModel = new ReviewViewModel
        {
            ListingId = listing.Id,
            ConversationId = conversation.Id,
            ListingTitle = listing.Title,
            OtherUserId = otherUser.Id,
            OtherUserDisplayName = otherUser.DisplayName,
            IsBuyerReviewingSeller = isBuyerReviewingSeller
        };

        return View(viewModel);
    }

    /// <summary>
    /// 取得或建立對話（確保同一對用戶對同一商品只有一個對話）
    /// </summary>
    private async Task<(Conversation conversation, bool isNew)> GetOrCreateConversationAsync(string userId1, string userId2, Guid listingId)
    {
        // 確保 Participant1Id < Participant2Id，以便統一查詢
        var participant1Id = string.Compare(userId1, userId2, StringComparison.Ordinal) < 0 ? userId1 : userId2;
        var participant2Id = string.Compare(userId1, userId2, StringComparison.Ordinal) < 0 ? userId2 : userId1;

        // 查詢相同用戶和相同商品的對話
        // 因為已經確保 participant1Id < participant2Id，所以只需要檢查一種順序
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

    /// <summary>
    /// 標記對話為已讀（API 端點）
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead([FromBody] Guid conversationId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return JsonError("未登入");
        }

        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null)
        {
            return Json(new { success = false, error = "找不到對話" });
        }

        // 驗證當前用戶是否為對話參與者
        if (conversation.Participant1Id != currentUser.Id && conversation.Participant2Id != currentUser.Id)
        {
            return Json(new { success = false, error = "無權限訪問此對話" });
        }

        // 更新當前用戶的最後已讀時間
        var now = TaiwanTime.Now;
        if (conversation.Participant1Id == currentUser.Id)
        {
            conversation.Participant1LastReadAt = now;
        }
        else
        {
            conversation.Participant2LastReadAt = now;
        }

        await _db.SaveChangesAsync();

        return Json(new { success = true });
    }
}

