using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Services;

namespace NeighborGoods.Web.ViewComponents;

public class UnreadMessageCountViewComponent : ViewComponent
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMessageService _messageService;

    public UnreadMessageCountViewComponent(
        UserManager<ApplicationUser> userManager,
        IMessageService messageService)
    {
        _userManager = userManager;
        _messageService = messageService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = await _userManager.GetUserAsync(HttpContext.User);
        if (user == null)
        {
            return View(0);
        }

        var unreadCount = await _messageService.GetUnreadMessageCountAsync(user.Id);
        return View(unreadCount);
    }
}

