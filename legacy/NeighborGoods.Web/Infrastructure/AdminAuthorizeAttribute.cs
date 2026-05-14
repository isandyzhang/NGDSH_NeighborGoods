using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.Enums;

namespace NeighborGoods.Web.Infrastructure;

/// <summary>
/// 管理員授權屬性，檢查用戶是否為管理員角色
/// </summary>
public class AdminAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // 檢查是否已登入
        if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new RedirectToActionResult("Terms", "Home", null);
            return;
        }

        // 取得 UserManager
        var userManager = context.HttpContext.RequestServices
            .GetRequiredService<UserManager<ApplicationUser>>();
        
        var user = await userManager.GetUserAsync(context.HttpContext.User);
        
        // 檢查是否為管理員
        if (user == null || user.Role != UserRole.Admin)
        {
            context.Result = new RedirectToActionResult("Terms", "Home", null);
        }
    }
}

