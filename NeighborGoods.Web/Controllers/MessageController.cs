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
using NeighborGoods.Web.Services;
using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Controllers;

[Authorize]
public class MessageController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IHubContext<MessageHub> _hubContext;
    private readonly IMessageService _messageService;
    private readonly ILogger<MessageController> _logger;

    public MessageController(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IHubContext<MessageHub> hubContext,
        IMessageService messageService,
        ILogger<MessageController> logger)
        : base(userManager)
    {
        _db = db;
        _hubContext = hubContext;
        _messageService = messageService;
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

        // 使用服務層取得對話列表
        var viewModel = await _messageService.GetConversationsAsync(currentUser.Id);
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

        // 使用服務層取得對話詳情
        var viewModel = await _messageService.GetChatAsync(conversationId, currentUser.Id);

        if (viewModel == null)
        {
            return NotFound();
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

        // 使用服務層取得或建立對話
        var (conversation, isNew) = await _messageService.GetOrCreateConversationAsync(currentUser.Id, userId, listingId);

        // 如果是新建立的對話且需要發送初始訊息，自動發送系統訊息
        if (isNew && sendInitialMessage)
        {
            var result = await _messageService.SendMessageAsync(
                conversation.Id,
                null,
                null,
                "[系統發送] 請問商品還有嗎？",
                currentUser.Id);

            if (result.Success)
            {
            // 透過 SignalR 推送訊息給雙方參與者
            await _hubContext.Clients.Users(new[] { currentUser.Id, userId }).SendAsync(
                "ReceiveMessage",
                currentUser.Id,
                currentUser.DisplayName,
                    "[系統發送] 請問商品還有嗎？",
                    TaiwanTime.Now);
            }
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

        // 驗證接收者和商品（如果需要建立新對話）
        if (!model.ConversationId.HasValue && !string.IsNullOrEmpty(model.ReceiverId))
        {
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
        }

        // 使用服務層發送訊息
        var result = await _messageService.SendMessageAsync(
            model.ConversationId,
            model.ReceiverId,
            model.ListingId,
            model.Content,
            currentUser.Id);

        if (!result.Success)
        {
            if (isAjax)
            {
                return JsonError(result.ErrorMessage ?? "發送訊息時發生錯誤");
            }
            
            if (result.ErrorMessage == "找不到對話" || result.ErrorMessage == "無權限訪問此對話")
            {
                return Forbid();
            }
            
            return BadRequest(result.ErrorMessage ?? "發送訊息時發生錯誤");
        }

        // 取得對話以取得接收者 ID（用於 SignalR 通知）
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => 
                (model.ConversationId.HasValue && c.Id == model.ConversationId.Value) ||
                (!model.ConversationId.HasValue && !string.IsNullOrEmpty(model.ReceiverId) && 
                 ((c.Participant1Id == currentUser.Id && c.Participant2Id == model.ReceiverId) ||
                  (c.Participant2Id == currentUser.Id && c.Participant1Id == model.ReceiverId)) &&
                 c.ListingId == model.ListingId));

        if (conversation != null)
        {
        var receiverId = conversation.Participant1Id == currentUser.Id
            ? conversation.Participant2Id
            : conversation.Participant1Id;

            // 透過 SignalR 推送訊息給接收者
        await _hubContext.Clients.Users(new[] { currentUser.Id, receiverId }).SendAsync(
            "ReceiveMessage",
            currentUser.Id,
            currentUser.DisplayName,
                model.Content.Trim(),
                TaiwanTime.Now);
        }

        // 如果是 AJAX 請求，返回 JSON；否則重定向
        if (isAjax)
        {
            return Json(new { success = true, messageId = result.Data });
        }

        return RedirectToAction(nameof(Chat), new { conversationId = conversation?.Id ?? model.ConversationId });
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

        // 使用服務層標記已讀
        var result = await _messageService.MarkAsReadAsync(conversationId, currentUser.Id);

        if (!result.Success)
        {
            return Json(new { success = false, error = result.ErrorMessage ?? "標記已讀時發生錯誤" });
        }

        return Json(new { success = true });
    }
}

