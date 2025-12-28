using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
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
    private readonly IHubContext<MessageHub> _hubContext;
    private readonly IMessageService _messageService;
    private readonly ILogger<MessageController> _logger;

    public MessageController(
        UserManager<ApplicationUser> userManager,
        IHubContext<MessageHub> hubContext,
        IMessageService messageService,
        ILogger<MessageController> logger)
        : base(userManager)
    {
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

        // 使用服務層驗證商品是否存在
        var listingResult = await _messageService.ValidateListingAsync(listingId);
        if (!listingResult.Success || listingResult.Data == null)
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

            // 使用服務層驗證商品是否存在
            var listingResult = await _messageService.ValidateListingAsync(model.ListingId.Value);
            if (!listingResult.Success || listingResult.Data == null)
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

        // 使用服務層取得對話資訊（用於 SignalR 通知）
        var conversationInfoResult = await _messageService.GetConversationForSignalRAsync(
            model.ConversationId,
            currentUser.Id,
            model.ReceiverId,
            model.ListingId);

        if (conversationInfoResult.Success && conversationInfoResult.Data != null)
        {
            var receiverId = conversationInfoResult.Data.ReceiverId;

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

        return RedirectToAction(nameof(Chat), new { conversationId = conversationInfoResult.Data?.ConversationId ?? model.ConversationId });
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

        // 使用服務層發送購買請求
        var result = await _messageService.SendPurchaseRequestAsync(
            model.ConversationId,
            model.ListingId,
            currentUser.Id,
            model.IsFreeOrCharity);

        if (!result.Success)
        {
            return Json(new { success = false, error = result.ErrorMessage ?? "發送購買請求時發生錯誤" });
        }

        // 取得對話資訊以取得接收者 ID（用於 SignalR）
        var conversationInfoResult = await _messageService.GetConversationForSignalRAsync(
            model.ConversationId,
            currentUser.Id,
            null,
            model.ListingId);

        if (conversationInfoResult.Success && conversationInfoResult.Data != null)
        {
            var receiverId = conversationInfoResult.Data.ReceiverId;

            // 根據商品類型生成訊息內容
            var messageContent = model.IsFreeOrCharity
                ? "[系統發送] 我想索取此商品"
                : "[系統發送] 我想購買此商品";

            // 透過 SignalR 推送訊息給雙方參與者（包括發送者自己）
            await _hubContext.Clients.Users(new[] { currentUser.Id, receiverId }).SendAsync(
                "ReceiveMessage",
                currentUser.Id,
                currentUser.DisplayName,
                messageContent,
                TaiwanTime.Now);

            // 通知賣家刷新頁面以顯示「同意交易」按鈕
            await _hubContext.Clients.User(receiverId).SendAsync("RefreshChat", model.ConversationId);
        }

        return Json(new { success = true, messageId = result.Data });
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

        // 使用服務層同意交易
        var result = await _messageService.AcceptPurchaseAsync(
            model.ConversationId,
            model.ListingId,
            currentUser.Id);

        if (!result.Success || result.Data == null)
        {
            return Json(new { success = false, error = result.ErrorMessage ?? "同意交易時發生錯誤" });
        }

        // 透過 SignalR 推送訊息給雙方參與者
        await _hubContext.Clients.Users(new[] { currentUser.Id, result.Data.BuyerId }).SendAsync(
            "ReceiveMessage",
            currentUser.Id,
            currentUser.DisplayName,
            result.Data.MessageContent,
            result.Data.MessageCreatedAt);

        // 通知買家刷新頁面以顯示「完成交易」按鈕
        await _hubContext.Clients.User(result.Data.BuyerId).SendAsync("RefreshChat", model.ConversationId);

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

        // 使用服務層完成交易
        var result = await _messageService.CompleteTransactionAsync(
            model.ConversationId,
            model.ListingId,
            currentUser.Id);

        if (!result.Success || result.Data == null)
        {
            return Json(new { success = false, error = result.ErrorMessage ?? "完成交易時發生錯誤" });
        }

        // 透過 SignalR 推送訊息給雙方參與者
        await _hubContext.Clients.Users(new[] { currentUser.Id, result.Data.SellerId }).SendAsync(
            "ReceiveMessage",
            currentUser.Id,
            currentUser.DisplayName,
            result.Data.MessageContent,
            result.Data.MessageCreatedAt);

        // 通知賣家刷新頁面以更新商品狀態
        await _hubContext.Clients.User(result.Data.SellerId).SendAsync("RefreshChat", model.ConversationId);

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

        // 使用服務層取得評價資訊
        var result = await _messageService.GetReviewInfoAsync(listingId, conversationId, currentUser.Id);

        if (!result.Success || result.Data == null)
        {
            if (result.ErrorMessage == "找不到對話" || result.ErrorMessage == "找不到商品" || result.ErrorMessage == "找不到對方用戶")
            {
                return NotFound();
            }
            if (result.ErrorMessage == "無權限訪問此對話")
            {
                return Forbid();
            }

            TempData["ErrorMessage"] = result.ErrorMessage ?? "取得評價資訊時發生錯誤";
            return RedirectToAction(nameof(Conversations));
        }

        return View(result.Data);
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

