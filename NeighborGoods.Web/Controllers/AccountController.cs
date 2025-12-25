using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Web.Data;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Models.ViewModels;
using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AppDbContext _db;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AppDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
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
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.UserName,
            DisplayName = model.DisplayName,
            Email = null, // 之後如需 Email 再擴充
            CreatedAt = TaiwanTime.Now
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Home");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
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
            lockoutOnFailure: false);

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
            ModelState.AddModelError(string.Empty, "帳號已被鎖定，請稍後再試。");
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

        var createResult = await _userManager.CreateAsync(user);
        if (createResult.Succeeded)
        {
            var loginResult = await _userManager.AddLoginAsync(user, info);
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
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        // 查詢統計數據
        var totalListings = await _db.Listings
            .CountAsync(l => l.SellerId == user.Id);

        var activeListings = await _db.Listings
            .CountAsync(l => l.SellerId == user.Id && l.Status == ListingStatus.Active);

        var completedListings = await _db.Listings
            .CountAsync(l => l.SellerId == user.Id && 
                           (l.Status == ListingStatus.Sold || l.Status == ListingStatus.Donated));

        var viewModel = new ProfileViewModel
        {
            User = user,
            TotalListings = totalListings,
            ActiveListings = activeListings,
            CompletedListings = completedListings
        };

        return View(viewModel);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        // 刪除用戶（會透過 Cascade Delete 自動刪除相關的 Listings 和 ListingImages）
        var result = await _userManager.DeleteAsync(user);
        if (result.Succeeded)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // 如果刪除失敗，返回個人資料頁面並顯示錯誤
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        // 重新載入統計數據
        var totalListings = await _db.Listings
            .CountAsync(l => l.SellerId == user.Id);

        var activeListings = await _db.Listings
            .CountAsync(l => l.SellerId == user.Id && l.Status == ListingStatus.Active);

        var completedListings = await _db.Listings
            .CountAsync(l => l.SellerId == user.Id && 
                           (l.Status == ListingStatus.Sold || l.Status == ListingStatus.Donated));

        var viewModel = new ProfileViewModel
        {
            User = user,
            TotalListings = totalListings,
            ActiveListings = activeListings,
            CompletedListings = completedListings
        };

        return View("Profile", viewModel);
    }
}


