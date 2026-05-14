using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NeighborGoods.Web.Infrastructure;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Models.ViewModels;
using NeighborGoods.Web.Services;

namespace NeighborGoods.Web.Controllers;

[Authorize]
public class ContactAdminController : BaseController
{
    private readonly IEmailNotificationService? _emailNotificationService;
    private readonly ILogger<ContactAdminController> _logger;
    private const string AdminEmail = "isandyzhang@gmail.com";

    public ContactAdminController(
        UserManager<ApplicationUser> userManager,
        IEmailNotificationService? emailNotificationService,
        ILogger<ContactAdminController> logger)
        : base(userManager)
    {
        _emailNotificationService = emailNotificationService;
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

        try
        {
            if (_emailNotificationService == null)
            {
                _logger.LogWarning("Email 通知服務未設定，無法發送聯絡管理員郵件");
                ModelState.AddModelError(string.Empty, "目前無法發送訊息，請稍後再試");
                return View("Index", model);
            }

            // 建立郵件內容
            var emailMessage = $@"用戶聯絡管理員通知

來自用戶：{user.DisplayName ?? user.UserName}
用戶 Email：{user.Email}
用戶 ID：{user.Id}

訊息內容：
{model.Content}

---
此郵件由系統自動發送
時間：{DateTime.UtcNow.AddHours(8):yyyy-MM-dd HH:mm:ss} (台灣時間)";

            // 發送郵件到管理員信箱
            await _emailNotificationService.SendPushMessageAsync(
                AdminEmail,
                emailMessage,
                NotificationPriority.High);

            _logger.LogInformation(
                "用戶聯絡管理員郵件已發送：UserId={UserId}, UserEmail={UserEmail}, AdminEmail={AdminEmail}",
                user.Id, user.Email, AdminEmail);

            TempData["SuccessMessage"] = "訊息已發送給管理員";
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送聯絡管理員郵件時發生錯誤：UserId={UserId}", user.Id);
            ModelState.AddModelError(string.Empty, "發送訊息時發生錯誤，請稍後再試");
            return View("Index", model);
        }
    }
}

