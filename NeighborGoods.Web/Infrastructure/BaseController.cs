using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NeighborGoods.Web.Models.Entities;

namespace NeighborGoods.Web.Infrastructure;

/// <summary>
/// 所有控制器的基礎類別，提供常用的輔助方法
/// </summary>
public abstract class BaseController : Controller
{
    protected readonly UserManager<ApplicationUser> UserManager;
    
    protected BaseController(UserManager<ApplicationUser> userManager)
    {
        UserManager = userManager;
    }
    
    /// <summary>
    /// 取得當前登入的用戶，如果未登入則返回 null
    /// </summary>
    protected async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        return await UserManager.GetUserAsync(User);
    }
    
    /// <summary>
    /// 取得當前登入的用戶，如果未登入則拋出異常
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">當用戶未登入時拋出</exception>
    protected async Task<ApplicationUser> RequireCurrentUserAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            throw new UnauthorizedAccessException("用戶未登入");
        }
        return user;
    }
    
    /// <summary>
    /// 返回統一的 JSON 錯誤回應格式
    /// </summary>
    protected IActionResult JsonError(string message)
    {
        return Json(new { success = false, error = message });
    }
    
    /// <summary>
    /// 返回統一的 JSON 成功回應格式
    /// </summary>
    protected IActionResult JsonSuccess(object? data = null)
    {
        return Json(new { success = true, data });
    }

    /// <summary>
    /// 在每次 Action 執行前，為已登入使用者設定與 Email 相關的 ViewBag 旗標，
    /// 供版面（例如 _Layout）決定是否顯示提醒。
    /// </summary>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 預設值（未登入或找不到使用者時）
        ViewBag.HasEmail = false;
        ViewBag.IsEmailConfirmed = false;
        ViewBag.ShouldShowEmailReminder = false;

        if (User?.Identity?.IsAuthenticated ?? false)
        {
            var user = await GetCurrentUserAsync();
            if (user != null)
            {
                var hasEmail = !string.IsNullOrEmpty(user.Email);
                var isEmailConfirmed = user.EmailConfirmed;

                // 與原本 HomeController 的語意相容：HasEmail 表示「有 Email 且已驗證」
                ViewBag.HasEmail = hasEmail && isEmailConfirmed;
                ViewBag.IsEmailConfirmed = isEmailConfirmed;

                // 只要沒填 Email 或尚未驗證，就顯示提醒
                ViewBag.ShouldShowEmailReminder = !hasEmail || !isEmailConfirmed;
            }
        }

        await next();
    }
}

