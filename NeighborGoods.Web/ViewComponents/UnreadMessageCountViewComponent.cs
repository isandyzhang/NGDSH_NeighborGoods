using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Web.Data;
using NeighborGoods.Web.Models.Entities;

namespace NeighborGoods.Web.ViewComponents;

public class UnreadMessageCountViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public UnreadMessageCountViewComponent(
        AppDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = await _userManager.GetUserAsync(HttpContext.User);
        if (user == null)
        {
            return View(0);
        }

        // 使用單一查詢計算所有對話的未讀數量
        // 分別處理 Participant1 和 Participant2 的情況
        var unreadCount1 = await (
            from c in _db.Conversations
            where c.Participant1Id == user.Id
            from m in _db.Messages
            where m.ConversationId == c.Id
                && m.SenderId != user.Id
                && (c.Participant1LastReadAt == null || m.CreatedAt > c.Participant1LastReadAt.Value)
            select m
        ).CountAsync();

        var unreadCount2 = await (
            from c in _db.Conversations
            where c.Participant2Id == user.Id
            from m in _db.Messages
            where m.ConversationId == c.Id
                && m.SenderId != user.Id
                && (c.Participant2LastReadAt == null || m.CreatedAt > c.Participant2LastReadAt.Value)
            select m
        ).CountAsync();

        var unreadCount = unreadCount1 + unreadCount2;

        return View(unreadCount);
    }
}

