using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Web.Data;
using NeighborGoods.Web.Infrastructure;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Models.ViewModels;
using NeighborGoods.Web.Services;
using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Controllers;

public class AccountController : BaseController
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AppDbContext _db;
    private readonly IUserService _userService;
    private readonly IReviewService _reviewService;
    private readonly ILineMessagingApiService? _lineMessagingApiService;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AppDbContext db,
        IUserService userService,
        IReviewService reviewService,
        ILineMessagingApiService? lineMessagingApiService = null)
        : base(userManager)
    {
        _signInManager = signInManager;
        _db = db;
        _userService = userService;
        _reviewService = reviewService;
        _lineMessagingApiService = lineMessagingApiService;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("Register")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // 使用服務層註冊用戶
        var result = await _userService.RegisterUserAsync(model);
        if (result.Success && result.Data != null)
        {
            // SignIn 保留在 Controller 層（屬於認證流程）
            await _signInManager.SignInAsync(result.Data, isPersistent: false);
            return RedirectToAction("Index", "Home");
        }

        // 處理錯誤
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage);
        }

        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("Login")]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            model.UserName,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true); // 啟用帳號鎖定

        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        if (result.IsLockedOut)
        {
            // 取得用戶資訊以顯示鎖定結束時間
            var user = await UserManager.FindByNameAsync(model.UserName);
            if (user != null && user.LockoutEnd.HasValue)
            {
                var lockoutEnd = user.LockoutEnd.Value;
                if (lockoutEnd > DateTimeOffset.UtcNow)
                {
                    var remainingTime = lockoutEnd - DateTimeOffset.UtcNow;
                    var remainingMinutes = (int)remainingTime.TotalMinutes;
                    ModelState.AddModelError(string.Empty, 
                        $"帳號已被鎖定，請在 {remainingMinutes} 分鐘後再試。");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "帳號已被鎖定，請稍後再試。");
                }
            }
            else
            {
                ModelState.AddModelError(string.Empty, "帳號已被鎖定，請稍後再試。");
            }
        }
        else
        {
            ModelState.AddModelError(string.Empty, "帳號或密碼錯誤。");
        }

        return View(model);
    }

    /// <summary>
    /// 導向到外部登入提供者（例如 LINE）。
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        // 組成回呼網址：/Account/ExternalLoginCallback
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    /// <summary>
    /// 外部登入完成後的回呼（LINE 會導回這裡，再由 SignInManager 處理）。
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        returnUrl ??= Url.Content("~/");
        ViewData["ReturnUrl"] = returnUrl;

        if (!string.IsNullOrEmpty(remoteError))
        {
            ModelState.AddModelError(string.Empty, $"外部登入發生錯誤：{remoteError}");
            return View("Login", new LoginViewModel());
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            ModelState.AddModelError(string.Empty, "無法取得外部登入資訊，請再試一次。");
            return View("Login", new LoginViewModel());
        }

        // 1. 嘗試以外部登入直接登入（如果之前已綁定過）
        var signInResult = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: false,
            bypassTwoFactor: true);

        if (signInResult.Succeeded)
        {
            return LocalRedirect(returnUrl);
        }

        // 2. 尚未綁定：建立新的本地使用者帳號，並綁定此外部登入
        var lineUserId = info.Principal.FindFirst("sub")?.Value ?? info.ProviderKey;
        var displayName = info.Principal.Identity?.Name
                          ?? info.Principal.FindFirst("name")?.Value
                          ?? "LINE 使用者";

        var user = new ApplicationUser
        {
            UserName = $"line_{lineUserId}",
            DisplayName = displayName,
            LineUserId = lineUserId,
            Email = null,
            CreatedAt = TaiwanTime.Now
        };

        var createResult = await UserManager.CreateAsync(user);
        if (createResult.Succeeded)
        {
            var loginResult = await UserManager.AddLoginAsync(user, info);
            if (loginResult.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                return LocalRedirect(returnUrl);
            }

            foreach (var error in loginResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
        else
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View("Login", new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Challenge();
        }

        // 使用服務層取得統計數據
        var statistics = await _userService.GetUserStatisticsAsync(user.Id);

        var viewModel = new ProfileViewModel
        {
            User = user,
            TotalListings = statistics.TotalListings,
            ActiveListings = statistics.ActiveListings,
            CompletedListings = statistics.CompletedListings
        };

        return View(viewModel);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Challenge();
        }

        // 使用服務層刪除用戶
        var result = await _userService.DeleteUserAsync(user.Id);
        if (result.Success)
        {
            // SignOut 保留在 Controller 層（屬於認證流程）
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // 如果刪除失敗，返回個人資料頁面並顯示錯誤
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage);
        }

        // 重新載入統計數據
        var statistics = await _userService.GetUserStatisticsAsync(user.Id);

        var viewModel = new ProfileViewModel
        {
            User = user,
            TotalListings = statistics.TotalListings,
            ActiveListings = statistics.ActiveListings,
            CompletedListings = statistics.CompletedListings
        };

        return View("Profile", viewModel);
    }

    /// <summary>
    /// 提交評價
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(SubmitReviewViewModel model)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest("評價資料無效");
        }

        // 使用服務層提交評價
        var result = await _reviewService.SubmitReviewAsync(model, currentUser.Id);

        if (!result.Success)
        {
            if (result.ErrorMessage == "找不到商品" || result.ErrorMessage == "找不到對話")
            {
                return NotFound(result.ErrorMessage);
            }
            if (result.ErrorMessage == "無權限訪問此對話")
            {
                return Forbid();
            }

            // 如果是 AJAX 請求，返回 JSON；否則返回 BadRequest
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = false, error = result.ErrorMessage });
            }
            return BadRequest(result.ErrorMessage);
        }

        // 如果是 AJAX 請求，返回 JSON；否則重定向
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { success = true });
        }

        return RedirectToAction("Chat", "Message", new { conversationId = model.ConversationId });
    }

    /// <summary>
    /// 顯示用戶個人檔案頁面（包含成交紀錄和評分）
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> SellerProfile(string sellerId)
    {
        if (string.IsNullOrEmpty(sellerId))
        {
            return NotFound();
        }

        var seller = await UserManager.FindByIdAsync(sellerId);
        if (seller == null)
        {
            return NotFound();
        }

        // 使用服務層取得賣家檔案
        var viewModel = await _reviewService.GetSellerProfileAsync(sellerId);
        if (viewModel == null)
        {
            return NotFound();
        }

        // 填入賣家顯示名稱
        viewModel.SellerId = seller.Id;
        viewModel.SellerDisplayName = seller.DisplayName;

        return View(viewModel);
    }

    /// <summary>
    /// LINE Messaging API Webhook 處理
    /// </summary>
    [HttpPost("/Account/LineMessagingApiWebhook")]
    [IgnoreAntiforgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> LineMessagingApiWebhook()
    {
        if (_lineMessagingApiService == null)
        {
            return BadRequest("LINE Messaging API 服務未啟用");
        }

        try
        {
            // 讀取請求內容
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            // 驗證簽章
            var signature = Request.Headers["X-Line-Signature"].ToString();
            if (string.IsNullOrEmpty(signature) || !_lineMessagingApiService.ValidateWebhookSignature(body, signature))
            {
                return Unauthorized("簽章驗證失敗");
            }

            // 解析事件
            var events = _lineMessagingApiService.ParseWebhookEvents(body);

            foreach (var evt in events)
            {
                if (evt.Type == "follow" && !string.IsNullOrEmpty(evt.UserId))
                {
                    // 用戶加入 Bot
                    // 注意：這裡需要知道是哪個用戶加入的，但 Webhook 只提供 LINE User ID
                    // 實際綁定應該在用戶掃描 QR Code 後，透過其他機制完成
                    // 這裡可以記錄日誌或發送歡迎訊息
                    // 實際綁定邏輯應該在用戶授權流程中處理
                }
                else if (evt.Type == "unfollow" && !string.IsNullOrEmpty(evt.UserId))
                {
                    // 用戶封鎖 Bot
                    // 找出對應的用戶並解除綁定
                    var user = await _db.Users
                        .FirstOrDefaultAsync(u => u.LineMessagingApiUserId == evt.UserId);

                    if (user != null)
                    {
                        await _userService.UnbindLineMessagingApiAsync(user.Id);
                    }
                }
            }

            return Ok();
        }
        catch (Exception)
        {
            // 處理 Webhook 時發生錯誤
            return StatusCode(500, "處理 Webhook 時發生錯誤");
        }
    }

    /// <summary>
    /// 顯示 LINE Messaging API 授權頁面（QR Code）
    /// </summary>
    [HttpGet]
    [Authorize]
    public IActionResult AuthorizeLineMessagingApi()
    {
        // 產生 QR Code 或提供 Bot 連結
        // 這裡需要從設定檔取得 Bot ID 或 Channel ID
        // 簡化實作：提供連結格式
        var botId = "2008787056"; // TODO: 從設定檔取得
        var botLink = $"line://ti/p/@{botId}";
        var qrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=300x300&data={Uri.EscapeDataString(botLink)}";

        ViewBag.BotLink = botLink;
        ViewBag.QrCodeUrl = qrCodeUrl;

        return View();
    }

    /// <summary>
    /// 處理 LINE Messaging API 綁定（從 Webhook 接收 follow 事件後，用戶點擊確認綁定）
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmLineMessagingApiBinding(string lineUserId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrEmpty(lineUserId))
        {
            return BadRequest("LINE User ID 不能為空");
        }

        var result = await _userService.BindLineMessagingApiAsync(currentUser.Id, lineUserId);
        if (result.Success)
        {
            // 發送歡迎訊息
            if (_lineMessagingApiService != null)
            {
                try
                {
                    await _lineMessagingApiService.SendPushMessageAsync(
                        lineUserId,
                        "歡迎使用 LINE 通知功能！您現在可以透過 LINE 接收訊息通知。",
                        Models.Enums.NotificationPriority.Low);
                }
                catch (Exception)
                {
                    // 發送歡迎訊息失敗，但不影響綁定流程
                }
            }

            TempData["SuccessMessage"] = "LINE 通知已啟用";
            return RedirectToAction(nameof(Profile));
        }

        TempData["ErrorMessage"] = result.ErrorMessage ?? "啟用 LINE 通知時發生錯誤";
        return RedirectToAction(nameof(Profile));
    }

    /// <summary>
    /// 解除 LINE Messaging API 綁定
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeLineMessagingApi()
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        var result = await _userService.UnbindLineMessagingApiAsync(currentUser.Id);
        if (result.Success)
        {
            TempData["SuccessMessage"] = "LINE 通知已停用";
        }
        else
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "停用 LINE 通知時發生錯誤";
        }

        return RedirectToAction(nameof(Profile));
    }

    /// <summary>
    /// 更新通知偏好設定
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateNotificationPreference(int preference)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        // 驗證偏好設定值（1=即時, 2=摘要, 3=僅重要, 4=關閉）
        if (preference < 1 || preference > 4)
        {
            TempData["ErrorMessage"] = "無效的通知偏好設定";
            return RedirectToAction(nameof(Profile));
        }

        var result = await _userService.UpdateNotificationPreferenceAsync(currentUser.Id, preference);
        if (result.Success)
        {
            TempData["SuccessMessage"] = "通知偏好設定已更新";
        }
        else
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "更新通知偏好設定時發生錯誤";
        }

        return RedirectToAction(nameof(Profile));
    }
}


