using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
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
    private readonly IUserService _userService;
    private readonly IReviewService _reviewService;
    private readonly ILineMessagingApiService? _lineMessagingApiService;
    private readonly IConfiguration _configuration;
    private readonly IEmailNotificationService? _emailNotificationService;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IUserService userService,
        IReviewService reviewService,
        IConfiguration configuration,
        ILineMessagingApiService? lineMessagingApiService = null,
        IEmailNotificationService? emailNotificationService = null)
        : base(userManager)
    {
        _signInManager = signInManager;
        _userService = userService;
        _reviewService = reviewService;
        _configuration = configuration;
        _lineMessagingApiService = lineMessagingApiService;
        _emailNotificationService = emailNotificationService;
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
            var user = result.Data;

            // 產生 Email 驗證 Token
            var token = await UserManager.GenerateEmailConfirmationTokenAsync(user);
            var callbackUrl = Url.Action(
                nameof(ConfirmEmail),
                "Account",
                new { userId = user.Id, token },
                protocol: Request.Scheme);

            // 寄送驗證信（若 Email 通知服務可用）
            if (!string.IsNullOrEmpty(user.Email) && _emailNotificationService != null)
            {
                var message = "感謝您註冊南港社宅社區專屬二手交易平台，請點擊下面的連結完成 Email 驗證：";
                var linkText = "點此完成 Email 驗證";
                await _emailNotificationService.SendPushMessageWithLinkAsync(
                    user.Email,
                    message,
                    callbackUrl ?? string.Empty,
                    linkText,
                    NeighborGoods.Web.Models.Enums.NotificationPriority.High);
            }

            // 顯示提示：請前往信箱完成驗證
            TempData["SuccessMessage"] = "註冊成功，請前往您的 Email 收信並完成驗證後再登入。";
            return RedirectToAction(nameof(Login));
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

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            TempData["ErrorMessage"] = "Email 驗證連結無效，請重新嘗試或要求重寄驗證信。";
            return RedirectToAction(nameof(Login));
        }

        var user = await UserManager.FindByIdAsync(userId);
        if (user == null)
        {
            TempData["ErrorMessage"] = "找不到對應的帳號，請確認連結是否正確。";
            return RedirectToAction(nameof(Login));
        }

        if (user.EmailConfirmed)
        {
            TempData["InfoMessage"] = "您的 Email 已經完成驗證，可以直接登入。";
            return RedirectToAction(nameof(Login));
        }

        var result = await UserManager.ConfirmEmailAsync(user, token);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Email 驗證成功，現在可以使用帳號密碼登入，並刊登商品。";
        }
        else
        {
            TempData["ErrorMessage"] = "Email 驗證失敗，連結可能已過期或無效，請重新登入後要求重寄驗證信。";
        }

        return RedirectToAction(nameof(Login));
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
            // 成功登入後，若該帳號的 Email 尚未驗證，立即登出並提示
            var signedInUser = await UserManager.FindByNameAsync(model.UserName);
            if (signedInUser != null && !signedInUser.EmailConfirmed)
            {
                await _signInManager.SignOutAsync();
                ModelState.AddModelError(string.Empty, "此帳號的 Email 尚未完成驗證，請先前往信箱完成驗證後再登入。");
                return View(model);
            }

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
            isPersistent: true,
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
                await _signInManager.SignInAsync(user, isPersistent: true);
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
                    var existingUser = await _userService.GetUserByLineMessagingApiUserIdAsync(lineUserId);
                    
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
                        var pendingBindings = await _userService.GetLineBindingPendingByLineUserIdAsync(null);
                        
                        if (pendingBindings.Count == 1)
                        {
                            // 只有一筆記錄，直接更新
                            var pending = pendingBindings.First();
                            var updateResult = await _userService.UpdateLineBindingPendingLineUserIdAsync(pending.Id, lineUserId);
                            
                            if (updateResult.Success)
                            {
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
                    var user = await _userService.GetUserByLineMessagingApiUserIdAsync(evt.UserId);

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

        // 使用服務層建立暫存記錄
        var result = await _userService.CreateLineBindingPendingAsync(currentUser.Id, token);
        if (!result.Success || result.Data == null)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "建立綁定記錄時發生錯誤";
            return RedirectToAction(nameof(Profile));
        }

        var pendingBinding = result.Data;

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
    /// 啟用 Email 通知
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableEmailNotification()
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Challenge();
        }

        var result = await _userService.EnableEmailNotificationAsync(currentUser.Id);
        if (result.Success)
        {
            TempData["SuccessMessage"] = "Email 通知已啟用";
        }
        else
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "啟用 Email 通知時發生錯誤";
        }

        return RedirectToAction(nameof(Profile));
    }

    /// <summary>
    /// 停用 Email 通知
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableEmailNotification()
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Challenge();
        }

        var result = await _userService.DisableEmailNotificationAsync(currentUser.Id);
        if (result.Success)
        {
            TempData["SuccessMessage"] = "Email 通知已停用";
        }
        else
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "停用 Email 通知時發生錯誤";
        }

        return RedirectToAction(nameof(Profile));
    }

    /// <summary>
    /// 設定 Email 並啟用通知（AJAX，不需要驗證 Email）
    /// </summary>
    [HttpPost]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SetEmailAndEnableNotification([FromBody] SetEmailRequest request)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Json(new { success = false, message = "未登入" });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Json(new { success = false, message = "Email 不能為空" });
        }

        // 簡單的 Email 格式驗證
        if (!request.Email.Contains("@") || !request.Email.Contains("."))
        {
            return Json(new { success = false, message = "Email 格式不正確" });
        }

        var result = await _userService.SetEmailAndEnableNotificationAsync(currentUser.Id, request.Email.Trim());
        if (result.Success)
        {
            return Json(new { success = true, message = "Email 通知已啟用" });
        }

        return Json(new { success = false, message = result.ErrorMessage ?? "設定 Email 時發生錯誤" });
    }

    /// <summary>
    /// 刊登商品用：發送 Email 驗證碼
    /// </summary>
    [HttpPost]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SendListingEmailCode([FromBody] SetEmailRequest request)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Json(new { success = false, message = "未登入" });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Json(new { success = false, message = "Email 不能為空" });
        }

        var email = request.Email.Trim();
        if (!email.Contains("@") || !email.Contains("."))
        {
            return Json(new { success = false, message = "Email 格式不正確" });
        }

        // 產生 6 位數驗證碼
        var random = new Random();
        var code = random.Next(100000, 999999).ToString();

        // 將驗證碼與 Email 暫存於 Session（僅用於本次瀏覽器會話）
        HttpContext.Session.SetString("ListingEmail", email);
        HttpContext.Session.SetString("ListingEmailCode", code);
        HttpContext.Session.SetString("ListingEmailCodeExpiresAt", DateTime.UtcNow.AddMinutes(10).ToString("O"));

        if (_emailNotificationService != null)
        {
            var message = $"您正在驗證刊登商品用的 Email，您的驗證碼為：{code}（10 分鐘內有效）。";
            await _emailNotificationService.SendPushMessageAsync(
                email,
                message,
                NeighborGoods.Web.Models.Enums.NotificationPriority.High);
        }

        return Json(new { success = true, message = "驗證碼已寄出，請至信箱查收。" });
    }

    /// <summary>
    /// 顯示刊登前 Email 驗證頁面
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> VerifyListingEmail(string? returnUrl = null)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Challenge();
        }

        // 如果已經有驗證過的 Email，就直接導回原頁
        if (!string.IsNullOrEmpty(currentUser.Email) && currentUser.EmailConfirmed)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        // 預設回到刊登頁
        if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            returnUrl = Url.Action("Create", "Listing");
        }

        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    /// <summary>
    /// 刊登商品用：驗證 Email 驗證碼並更新帳號 EmailConfirmed
    /// </summary>
    [HttpPost]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> VerifyListingEmailCode([FromBody] VerifyEmailCodeRequest request)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Json(new { success = false, message = "未登入" });
        }

        var sessionEmail = HttpContext.Session.GetString("ListingEmail");
        var sessionCode = HttpContext.Session.GetString("ListingEmailCode");
        var expiresAtString = HttpContext.Session.GetString("ListingEmailCodeExpiresAt");

        if (string.IsNullOrEmpty(sessionEmail) ||
            string.IsNullOrEmpty(sessionCode) ||
            string.IsNullOrEmpty(expiresAtString))
        {
            return Json(new { success = false, message = "請先寄送驗證碼。" });
        }

        if (!DateTime.TryParse(expiresAtString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiresAtUtc) ||
            DateTime.UtcNow > expiresAtUtc)
        {
            return Json(new { success = false, message = "驗證碼已過期，請重新寄送。" });
        }

        if (!string.Equals(sessionEmail, request.Email?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Json(new { success = false, message = "Email 不一致，請重新確認。" });
        }

        if (!string.Equals(sessionCode, request.Code?.Trim(), StringComparison.Ordinal))
        {
            return Json(new { success = false, message = "驗證碼錯誤，請重新輸入。" });
        }

        // 驗證成功：更新使用者 Email 與 EmailConfirmed
        currentUser.Email = sessionEmail;
        currentUser.EmailConfirmed = true;

        var updateResult = await UserManager.UpdateAsync(currentUser);
        if (!updateResult.Succeeded)
        {
            var error = string.Join(", ", updateResult.Errors.Select(e => e.Description));
            return Json(new { success = false, message = $"更新帳號 Email 時發生錯誤：{error}" });
        }

        // 清除 Session 中的驗證資訊
        HttpContext.Session.Remove("ListingEmail");
        HttpContext.Session.Remove("ListingEmailCode");
        HttpContext.Session.Remove("ListingEmailCodeExpiresAt");

        return Json(new { success = true, message = "Email 驗證成功，現在可以刊登商品了。" });
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

        // 使用服務層查詢暫存記錄
        var pending = await _userService.GetLineBindingPendingByUserIdAsync(currentUser.Id, pendingBindingId);

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

        // 使用服務層查詢暫存記錄
        var pending = await _userService.GetLineBindingPendingByUserIdAsync(currentUser.Id, pendingBindingId);

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
            await _userService.DeleteLineBindingPendingAsync(pending.Id);

            TempData["InfoMessage"] = "您已經綁定 LINE 通知功能";
            return RedirectToAction(nameof(Profile));
        }

        // 檢查 LINE User ID 是否已被其他用戶使用
        var lineUserIdExists = await _userService.CheckLineUserIdExistsAsync(pending.LineUserId, currentUser.Id);

        if (lineUserIdExists)
        {
            // 清除暫存記錄
            await _userService.DeleteLineBindingPendingAsync(pending.Id);

            TempData["ErrorMessage"] = "此 LINE 帳號已被其他用戶綁定";
            return RedirectToAction(nameof(AuthorizeLineMessagingApi));
        }

        // 完成綁定
        var result = await _userService.BindLineMessagingApiAsync(currentUser.Id, pending.LineUserId);
        if (result.Success)
        {
            // 清除暫存記錄
            await _userService.DeleteLineBindingPendingAsync(pending.Id);

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

/// <summary>
/// 設定 Email 請求
/// </summary>
public class SetEmailRequest
{
    public string Email { get; set; } = string.Empty;
}

public class VerifyEmailCodeRequest
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}


