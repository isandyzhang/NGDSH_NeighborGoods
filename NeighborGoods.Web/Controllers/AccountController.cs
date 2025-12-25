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
using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Controllers;

public class AccountController : BaseController
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AppDbContext _db;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AppDbContext db)
        : base(userManager)
    {
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
    [EnableRateLimiting("Register")]
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

        var result = await UserManager.CreateAsync(user, model.Password);
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
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Challenge();
        }

        // 刪除用戶（會透過 Cascade Delete 自動刪除相關的 Listings 和 ListingImages）
        var result = await UserManager.DeleteAsync(user);
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

        // 驗證評分範圍
        if (model.Rating < 1 || model.Rating > 5)
        {
            return BadRequest("評分必須在 1-5 之間");
        }

        // 驗證商品
        var listing = await _db.Listings
            .FirstOrDefaultAsync(l => l.Id == model.ListingId);

        if (listing == null)
        {
            return NotFound("找不到商品");
        }

        // 驗證商品狀態為已售出
        if (listing.Status != ListingStatus.Sold && listing.Status != ListingStatus.Donated)
        {
            return BadRequest("只有已售出或已捐贈的商品才能評價");
        }

        // 驗證對話
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == model.ConversationId);

        if (conversation == null)
        {
            return NotFound("找不到對話");
        }

        // 驗證當前用戶是否為對話參與者
        if (conversation.Participant1Id != currentUser.Id && conversation.Participant2Id != currentUser.Id)
        {
            return Forbid();
        }

        // 判斷當前用戶是買家還是賣家
        var isBuyer = listing.SellerId != currentUser.Id;
        
        // 確定被評價者
        var targetUserId = isBuyer 
            ? listing.SellerId  // 買家評價賣家
            : (conversation.Participant1Id == currentUser.Id ? conversation.Participant2Id : conversation.Participant1Id); // 賣家評價買家
        
        // 檢查是否已經評價過
        var existingReview = await _db.Reviews
            .FirstOrDefaultAsync(r => r.ListingId == model.ListingId && r.BuyerId == currentUser.Id);

        if (existingReview != null)
        {
            // 更新現有評價
            existingReview.Rating = model.Rating;
            existingReview.Content = model.Content?.Trim();
            existingReview.CreatedAt = TaiwanTime.Now;
        }
        else
        {
            // 創建新評價
            var review = new Review
            {
                Id = Guid.NewGuid(),
                ListingId = listing.Id,
                SellerId = targetUserId, // 被評價者
                BuyerId = currentUser.Id, // 評價者
                Rating = model.Rating,
                Content = model.Content?.Trim(),
                CreatedAt = TaiwanTime.Now
            };

            _db.Reviews.Add(review);
        }

        await _db.SaveChangesAsync();

        // 如果是 AJAX 請求，返回 JSON；否則重定向
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { success = true });
        }

        return RedirectToAction("Chat", "Message", new { conversationId = model.ConversationId });
    }

    /// <summary>
    /// 顯示賣家資料頁面
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

        // 查詢賣家的所有有評價的交易（透過 Review 記錄查詢）
        var reviews = await _db.Reviews
            .Include(r => r.Listing)
            .Include(r => r.Buyer)
            .Where(r => r.SellerId == sellerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        // 計算統計數據
        var totalCompletedTransactions = reviews.Count;
        var averageRating = totalCompletedTransactions > 0
            ? reviews.Average(r => (double)r.Rating)
            : 0.0;

        // 構建成交紀錄列表
        var completedTransactions = reviews.Select(r => new CompletedTransactionItem
        {
            ListingId = r.ListingId,
            ListingTitle = r.Listing?.Title ?? "未知商品",
            Price = r.Listing?.Price,
            IsFree = r.Listing?.IsFree ?? false,
            Rating = r.Rating,
            ReviewContent = r.Content,
            CompletedAt = r.CreatedAt,
            BuyerDisplayName = r.Buyer?.DisplayName ?? "未知買家"
        }).ToList();

        var viewModel = new SellerProfileViewModel
        {
            SellerId = seller.Id,
            SellerDisplayName = seller.DisplayName,
            TotalCompletedTransactions = totalCompletedTransactions,
            AverageRating = averageRating,
            CompletedTransactions = completedTransactions
        };

        return View(viewModel);
    }
}


