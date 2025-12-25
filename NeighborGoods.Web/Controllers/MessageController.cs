using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Web.Data;
using NeighborGoods.Web.Hubs;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.ViewModels;
using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Controllers;

[Authorize]
public class MessageController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<MessageHub> _hubContext;
    private readonly ILogger<MessageController> _logger;

    public MessageController(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IHubContext<MessageHub> hubContext,
        ILogger<MessageController> logger)
    {
        _db = db;
        _userManager = userManager;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// 顯示對話列表
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Conversations()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        // 取得當前用戶參與的所有對話
        var conversations = await _db.Conversations
            .Include(c => c.Participant1)
            .Include(c => c.Participant2)
            .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
            .Where(c => c.Participant1Id == currentUser.Id || c.Participant2Id == currentUser.Id)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();

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
            var lastMessage = conversation.Messages
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefault();

            conversationItems.Add(new ConversationItemViewModel
            {
                ConversationId = conversation.Id,
                OtherUserId = otherUser.Id,
                OtherUserDisplayName = otherUser.DisplayName,
                LastMessage = lastMessage?.Content,
                LastMessageTime = lastMessage?.CreatedAt,
                UnreadCount = 0 // 目前不實作已讀功能
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
    public async Task<IActionResult> Chat(Guid conversationId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        var conversation = await _db.Conversations
            .Include(c => c.Participant1)
            .Include(c => c.Participant2)
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
            OtherUserId = otherUser.Id,
            OtherUserDisplayName = otherUser.DisplayName,
            Messages = messages
        };

        return View(viewModel);
    }

    /// <summary>
    /// 與特定用戶開始對話（如果對話不存在則建立）
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ChatWithUser(string userId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        if (currentUser.Id == userId)
        {
            return BadRequest("無法與自己對話");
        }

        var otherUser = await _userManager.FindByIdAsync(userId);
        if (otherUser == null)
        {
            return NotFound("找不到該用戶");
        }

        // 取得或建立對話
        var conversation = await GetOrCreateConversationAsync(currentUser.Id, userId);

        return RedirectToAction(nameof(Chat), new { conversationId = conversation.Id });
    }

    /// <summary>
    /// 發送訊息
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(SendMessageViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
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
                return NotFound("找不到對話");
            }

            // 驗證當前用戶是否為對話參與者
            if (conversation.Participant1Id != currentUser.Id && conversation.Participant2Id != currentUser.Id)
            {
                return Forbid();
            }
        }
        else if (!string.IsNullOrEmpty(model.ReceiverId))
        {
            // 建立新對話
            if (currentUser.Id == model.ReceiverId)
            {
                return BadRequest("無法與自己對話");
            }

            var receiver = await _userManager.FindByIdAsync(model.ReceiverId);
            if (receiver == null)
            {
                return NotFound("找不到接收者");
            }

            conversation = await GetOrCreateConversationAsync(currentUser.Id, model.ReceiverId);
        }
        else
        {
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

        await _hubContext.Clients.User(receiverId).SendAsync(
            "ReceiveMessage",
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
    /// 取得或建立對話（確保同一對用戶只有一個對話）
    /// </summary>
    private async Task<Conversation> GetOrCreateConversationAsync(string userId1, string userId2)
    {
        // 確保 Participant1Id < Participant2Id，以便統一查詢
        var participant1Id = string.Compare(userId1, userId2, StringComparison.Ordinal) < 0 ? userId1 : userId2;
        var participant2Id = string.Compare(userId1, userId2, StringComparison.Ordinal) < 0 ? userId2 : userId1;

        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c =>
                (c.Participant1Id == participant1Id && c.Participant2Id == participant2Id) ||
                (c.Participant1Id == participant2Id && c.Participant2Id == participant1Id));

        if (conversation == null)
        {
            conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                Participant1Id = participant1Id,
                Participant2Id = participant2Id,
                CreatedAt = TaiwanTime.Now,
                UpdatedAt = TaiwanTime.Now
            };

            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync();
        }

        return conversation;
    }
}

