using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NeighborGoods.Web.Infrastructure;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.ViewModels;
using NeighborGoods.Web.Services;

namespace NeighborGoods.Web.Controllers;

[Authorize]
public class ContactAdminController : BaseController
{
    private readonly IAdminService _adminService;
    private readonly ILogger<ContactAdminController> _logger;

    public ContactAdminController(
        UserManager<ApplicationUser> userManager,
        IAdminService adminService,
        ILogger<ContactAdminController> logger)
        : base(userManager)
    {
        _adminService = adminService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(new ContactAdminViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(ContactAdminViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Challenge();
        }

        var success = await _adminService.SendMessageToAdminAsync(user.Id, model.Content);

        if (success)
        {
            TempData["SuccessMessage"] = "訊息已發送給管理員";
            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty, "發送訊息時發生錯誤，請稍後再試");
        return View("Index", model);
    }
}

