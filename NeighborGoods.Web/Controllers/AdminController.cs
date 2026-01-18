using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NeighborGoods.Web.Constants;
using NeighborGoods.Web.Infrastructure;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Services;

namespace NeighborGoods.Web.Controllers;

[AdminAuthorize]
public class AdminController : BaseController
{
    private readonly IAdminService _adminService;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminController> _logger;
    private readonly IEmailNotificationService? _emailNotificationService;

    public AdminController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAdminService adminService,
        IConfiguration configuration,
        ILogger<AdminController> logger,
        IEmailNotificationService? emailNotificationService = null)
        : base(userManager)
    {
        _signInManager = signInManager;
        _adminService = adminService;
        _configuration = configuration;
        _logger = logger;
        _emailNotificationService = emailNotificationService;
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string password)
    {
        // 從設定檔取得管理員 UserId
        var adminUserId = _configuration["Admin:UserId"];
        if (string.IsNullOrEmpty(adminUserId))
        {
            TempData["ErrorMessage"] = "管理員設定錯誤";
            return RedirectToAction("Terms", "Home");
        }

        // 取得管理員用戶
        var adminUser = await UserManager.FindByIdAsync(adminUserId);
        if (adminUser == null)
        {
            TempData["ErrorMessage"] = "找不到管理員帳號";
            return RedirectToAction("Terms", "Home");
        }

        // 驗證密碼
        var isValid = await _adminService.VerifyPasswordAsync(password);
        if (!isValid)
        {
            TempData["ErrorMessage"] = "密碼錯誤";
            return RedirectToAction("Terms", "Home");
        }

        // 檢查是否為管理員角色
        if (adminUser.Role != UserRole.Admin)
        {
            TempData["ErrorMessage"] = "此帳號不是管理員";
            return RedirectToAction("Terms", "Home");
        }

        // 使用 Identity 登入
        await _signInManager.SignInAsync(adminUser, isPersistent: true);
        _logger.LogInformation("管理員登入成功：{UserId}", adminUser.Id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> Listings(int page = 1)
    {
        var viewModel = await _adminService.GetAllListingsAsync(page, PaginationConstants.DefaultPageSize);
        return View(viewModel);
    }

    public async Task<IActionResult> ListingDetails(Guid id)
    {
        var viewModel = await _adminService.GetListingDetailsWithConversationsAsync(id);
        
        if (viewModel == null)
        {
            TempData["ErrorMessage"] = "找不到商品";
            return RedirectToAction(nameof(Listings));
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteListing(Guid id)
    {
        var success = await _adminService.DeleteListingAsync(id);
        
        if (success)
        {
            TempData["SuccessMessage"] = "商品已刪除";
        }
        else
        {
            TempData["ErrorMessage"] = "刪除商品時發生錯誤";
        }

        return RedirectToAction(nameof(Listings));
    }

    public async Task<IActionResult> Users(int page = 1)
    {
        var viewModel = await _adminService.GetAllUsersAsync(page, PaginationConstants.DefaultPageSize);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var success = await _adminService.DeleteUserAsync(id);
        
        if (success)
        {
            TempData["SuccessMessage"] = "用戶已刪除";
        }
        else
        {
            TempData["ErrorMessage"] = "刪除用戶時發生錯誤";
        }

        return RedirectToAction(nameof(Users));
    }

    public async Task<IActionResult> Mailbox(int page = 1, bool? isRead = null)
    {
        var viewModel = await _adminService.GetAdminMessagesAsync(page, PaginationConstants.DefaultPageSize, isRead);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkMessageAsRead([FromBody] Guid id)
    {
        var success = await _adminService.MarkMessageAsReadAsync(id);
        
        if (success)
        {
            return Json(new { success = true });
        }

        return Json(new { success = false, error = "標記訊息時發生錯誤" });
    }

    /// <summary>
    /// 測試發送 Email 頁面
    /// </summary>
    public IActionResult TestEmail()
    {
        return View();
    }

    /// <summary>
    /// 發送測試 Email
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTestEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["ErrorMessage"] = "請輸入 Email 地址";
            return RedirectToAction(nameof(TestEmail));
        }

        // 簡單的 Email 格式驗證
        if (!email.Contains("@") || !email.Contains("."))
        {
            TempData["ErrorMessage"] = "Email 格式不正確";
            return RedirectToAction(nameof(TestEmail));
        }

        if (_emailNotificationService == null)
        {
            TempData["ErrorMessage"] = "Email 通知服務未設定，無法發送測試信";
            return RedirectToAction(nameof(TestEmail));
        }

        try
        {
            var testMessage = "這是一封測試信，用於驗證 Email 通知功能是否正常運作。";
            var testUrl = "https://neighborgoods.azurewebsites.net";
            var testLinkText = "前往 NeighborGoods";

            await _emailNotificationService.SendPushMessageWithLinkAsync(
                email.Trim(),
                testMessage,
                testUrl,
                testLinkText,
                NotificationPriority.Medium);

            _logger.LogInformation("管理員發送測試 Email 成功：{Email}", email);
            TempData["SuccessMessage"] = $"測試信已成功發送至 {email}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送測試 Email 時發生錯誤：{Email}", email);
            TempData["ErrorMessage"] = $"發送測試信時發生錯誤：{ex.Message}";
        }

        return RedirectToAction(nameof(TestEmail));
    }
}

