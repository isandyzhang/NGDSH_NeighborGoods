using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _configuration;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AppDbContext db,
        IUserService userService,
        IReviewService reviewService,
        IConfiguration configuration,
        ILineMessagingApiService? lineMessagingApiService = null)
        : base(userManager)
    {
        _signInManager = signInManager;
        _db = db;
        _userService = userService;
        _reviewService = reviewService;
        _configuration = configuration;
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
                    var lineUserId = evt.UserId;
                    
                    // 檢查用戶是否已經綁定過
                    var existingUser = await _db.Users
                        .FirstOrDefaultAsync(u => u.LineMessagingApiUserId == lineUserId);
                    
                    if (existingUser != null)
                    {
                        // 已經綁定過，發送歡迎訊息
                        if (_lineMessagingApiService != null)
                        {
                            try
                            {
                                await _lineMessagingApiService.SendPushMessageAsync(
                                    lineUserId,
                                    "歡迎回來！您已經綁定 LINE 通知功能。",
                                    Models.Enums.NotificationPriority.Low);
                            }
                            catch (Exception)
                            {
                                // 發送失敗不影響流程
                            }
                        }
                    }
                    else
                    {
                        // 查詢資料庫暫存表：找出所有「正在綁定」的記錄（LineUserId 為 null）
                        var pendingBindings = await _db.LineBindingPending
                            .Where(p => p.LineUserId == null)
                            .ToListAsync();
                        
                        if (pendingBindings.Count == 1)
                        {
                            // 只有一筆記錄，直接更新
                            var pending = pendingBindings.First();
                            pending.LineUserId = lineUserId;
                            await _db.SaveChangesAsync();
                            
                            // 發送歡迎訊息，提示用戶返回網站確認
                            if (_lineMessagingApiService != null)
                            {
                                try
                                {
                                    await _lineMessagingApiService.SendPushMessageAsync(
                                        lineUserId,
                                        "歡迎加入！請返回網站點擊「確認綁定」按鈕完成綁定。",
                                        Models.Enums.NotificationPriority.Low);
                                }
                                catch (Exception)
                                {
                                    // 發送失敗不影響流程
                                }
                            }
                        }
                        else if (pendingBindings.Count > 1)
                        {
                            // 有多筆記錄，發送訊息提示用戶返回網站完成綁定
                            if (_lineMessagingApiService != null)
                            {
                                try
                                {
                                    await _lineMessagingApiService.SendPushMessageAsync(
                                        lineUserId,
                                        "歡迎加入！請返回網站完成 LINE 通知綁定。",
                                        Models.Enums.NotificationPriority.Low);
                                }
                                catch (Exception)
                                {
                                    // 發送失敗不影響流程
                                }
                            }
                        }
                        else
                        {
                            // 沒有正在綁定的記錄，發送一般歡迎訊息
                            if (_lineMessagingApiService != null)
                            {
                                try
                                {
                                    await _lineMessagingApiService.SendPushMessageAsync(
                                        lineUserId,
                                        "歡迎加入！請前往網站個人資料頁面完成 LINE 通知綁定。",
                                        Models.Enums.NotificationPriority.Low);
                                }
                                catch (Exception)
                                {
                                    // 發送失敗不影響流程
                                }
                            }
                        }
                    }
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
    public async Task<IActionResult> AuthorizeLineMessagingApi()
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        // 檢查是否已經綁定過
        if (!string.IsNullOrEmpty(currentUser.LineMessagingApiUserId))
        {
            TempData["InfoMessage"] = "您已經綁定 LINE 通知功能";
            return RedirectToAction(nameof(Profile));
        }

        // 產生 Token
        var token = Guid.NewGuid().ToString("N"); // 32 字元，無連字號

        // 建立暫存記錄
        var pendingBinding = new Models.Entities.LineBindingPending
        {
            Id = Guid.NewGuid(),
            UserId = currentUser.Id,
            Token = token,
            LineUserId = null, // 還不知道
            CreatedAt = Utils.TaiwanTime.Now
        };

        _db.LineBindingPending.Add(pendingBinding);
        await _db.SaveChangesAsync();

        // 從設定檔取得 Bot ID
        var botId = _configuration["LineMessagingApi:BotId"] ?? "@559fslxw";
        
        // 確保 Bot ID 格式正確（如果有 @ 就保留，沒有就加上）
        if (!botId.StartsWith("@"))
        {
            botId = "@" + botId;
        }
        
        var botLink = $"line://ti/p/{botId}";
        var qrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=300x300&data={Uri.EscapeDataString(botLink)}";

        ViewBag.BotLink = botLink;
        ViewBag.QrCodeUrl = qrCodeUrl;
        ViewBag.PendingBindingId = pendingBinding.Id; // 用於後續查詢

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

    /// <summary>
    /// 檢查 LINE 綁定狀態（供前端 JavaScript 呼叫）
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> CheckLineBindingStatus(Guid? pendingBindingId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Json(new { success = false, message = "未登入" });
        }

        // 如果用戶已經綁定過，直接返回已完成
        if (!string.IsNullOrEmpty(currentUser.LineMessagingApiUserId))
        {
            return Json(new
            {
                success = true,
                status = "completed",
                message = "您已經綁定 LINE 通知功能"
            });
        }

        // 查詢暫存記錄
        Models.Entities.LineBindingPending? pending = null;
        if (pendingBindingId.HasValue)
        {
            pending = await _db.LineBindingPending
                .FirstOrDefaultAsync(p => p.Id == pendingBindingId.Value && p.UserId == currentUser.Id);
        }
        else
        {
            // 如果沒有提供 ID，查詢該用戶最新的暫存記錄
            pending = await _db.LineBindingPending
                .Where(p => p.UserId == currentUser.Id)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();
        }

        if (pending == null)
        {
            return Json(new
            {
                success = false,
                status = "not_found",
                message = "找不到綁定記錄，請重新開始"
            });
        }

        // 檢查是否已經有 LINE User ID（表示用戶已經加入 Bot）
        if (!string.IsNullOrEmpty(pending.LineUserId))
        {
            return Json(new
            {
                success = true,
                status = "ready",
                message = "已加入 Bot，請點擊確認綁定",
                lineUserId = pending.LineUserId
            });
        }

        return Json(new
        {
            success = true,
            status = "waiting",
            message = "正在等待加入 Bot..."
        });
    }

    /// <summary>
    /// 確認 LINE 綁定（使用 Token 機制）
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmLineBinding(Guid pendingBindingId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        // 查詢暫存記錄
        var pending = await _db.LineBindingPending
            .FirstOrDefaultAsync(p => p.Id == pendingBindingId && p.UserId == currentUser.Id);

        if (pending == null)
        {
            TempData["ErrorMessage"] = "找不到綁定記錄，請重新開始";
            return RedirectToAction(nameof(AuthorizeLineMessagingApi));
        }

        // 檢查是否已經有 LINE User ID
        if (string.IsNullOrEmpty(pending.LineUserId))
        {
            TempData["ErrorMessage"] = "尚未加入 Bot，請先掃描 QR Code 加入 Bot";
            return RedirectToAction(nameof(AuthorizeLineMessagingApi));
        }

        // 檢查用戶是否已經綁定過
        if (!string.IsNullOrEmpty(currentUser.LineMessagingApiUserId))
        {
            // 清除暫存記錄
            _db.LineBindingPending.Remove(pending);
            await _db.SaveChangesAsync();

            TempData["InfoMessage"] = "您已經綁定 LINE 通知功能";
            return RedirectToAction(nameof(Profile));
        }

        // 檢查 LINE User ID 是否已被其他用戶使用
        var existingUser = await _db.Users
            .FirstOrDefaultAsync(u => u.LineMessagingApiUserId == pending.LineUserId && u.Id != currentUser.Id);

        if (existingUser != null)
        {
            // 清除暫存記錄
            _db.LineBindingPending.Remove(pending);
            await _db.SaveChangesAsync();

            TempData["ErrorMessage"] = "此 LINE 帳號已被其他用戶綁定";
            return RedirectToAction(nameof(AuthorizeLineMessagingApi));
        }

        // 完成綁定
        var result = await _userService.BindLineMessagingApiAsync(currentUser.Id, pending.LineUserId);
        if (result.Success)
        {
            // 清除暫存記錄
            _db.LineBindingPending.Remove(pending);
            await _db.SaveChangesAsync();

            // 發送歡迎訊息
            if (_lineMessagingApiService != null)
            {
                try
                {
                    await _lineMessagingApiService.SendPushMessageAsync(
                        pending.LineUserId,
                        "綁定成功！您現在可以透過 LINE 接收訊息通知。",
                        Models.Enums.NotificationPriority.Low);
                }
                catch (Exception)
                {
                    // 發送失敗不影響綁定流程
                }
            }

            TempData["SuccessMessage"] = "LINE 通知已啟用";
            return RedirectToAction(nameof(Profile));
        }

        TempData["ErrorMessage"] = result.ErrorMessage ?? "綁定失敗，請稍後再試";
        return RedirectToAction(nameof(AuthorizeLineMessagingApi));
    }
}


