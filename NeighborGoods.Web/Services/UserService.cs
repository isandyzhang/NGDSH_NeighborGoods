using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeighborGoods.Web.Data;
using NeighborGoods.Web.Models.DTOs;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Models.ViewModels;
using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Services;

/// <summary>
/// 用戶服務實作
/// </summary>
public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UserService> _logger;

    public UserService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<UserService> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<UserStatistics> GetUserStatisticsAsync(string userId)
    {
        var totalListings = await _db.Listings
            .CountAsync(l => l.SellerId == userId);

        var activeListings = await _db.Listings
            .CountAsync(l => l.SellerId == userId && l.Status == ListingStatus.Active);

        var completedListings = await _db.Listings
            .CountAsync(l => l.SellerId == userId &&
                           (l.Status == ListingStatus.Sold || l.Status == ListingStatus.Donated));

        return new UserStatistics
        {
            TotalListings = totalListings,
            ActiveListings = activeListings,
            CompletedListings = completedListings
        };
    }

    public async Task<ServiceResult<ApplicationUser>> RegisterUserAsync(RegisterViewModel model)
    {
        try
        {
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
                return ServiceResult<ApplicationUser>.Ok(user);
            }

            var errorMessages = string.Join(", ", result.Errors.Select(e => e.Description));
            return ServiceResult<ApplicationUser>.Fail(errorMessages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "註冊用戶時發生錯誤");
            return ServiceResult<ApplicationUser>.Fail("註冊用戶時發生錯誤，請稍後再試");
        }
    }

    public async Task<ServiceResult> DeleteUserAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult.Fail("找不到用戶");
            }

            // 刪除用戶（會透過 Cascade Delete 自動刪除相關的 Listings 和 ListingImages）
            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                _logger.LogInformation("用戶 {UserId} 已刪除", userId);
                return ServiceResult.Ok();
            }

            var errorMessages = string.Join(", ", result.Errors.Select(e => e.Description));
            return ServiceResult.Fail(errorMessages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除用戶 {UserId} 時發生錯誤", userId);
            return ServiceResult.Fail("刪除用戶時發生錯誤，請稍後再試");
        }
    }

    public async Task<ServiceResult> BindLineMessagingApiAsync(string userId, string lineUserId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult.Fail("找不到用戶");
            }

            user.LineMessagingApiUserId = lineUserId;
            user.LineMessagingApiAuthorizedAt = TaiwanTime.Now;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                _logger.LogInformation("用戶 {UserId} 已綁定 LINE Messaging API，LineUserId={LineUserId}", userId, lineUserId);
                return ServiceResult.Ok();
            }

            var errorMessages = string.Join(", ", result.Errors.Select(e => e.Description));
            return ServiceResult.Fail(errorMessages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "綁定 LINE Messaging API 時發生錯誤：UserId={UserId}, LineUserId={LineUserId}", userId, lineUserId);
            return ServiceResult.Fail("綁定 LINE Messaging API 時發生錯誤，請稍後再試");
        }
    }

    public async Task<ServiceResult> UnbindLineMessagingApiAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult.Fail("找不到用戶");
            }

            user.LineMessagingApiUserId = null;
            user.LineMessagingApiAuthorizedAt = null;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                _logger.LogInformation("用戶 {UserId} 已解除 LINE Messaging API 綁定", userId);
                return ServiceResult.Ok();
            }

            var errorMessages = string.Join(", ", result.Errors.Select(e => e.Description));
            return ServiceResult.Fail(errorMessages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解除 LINE Messaging API 綁定時發生錯誤：UserId={UserId}", userId);
            return ServiceResult.Fail("解除綁定時發生錯誤，請稍後再試");
        }
    }

    public async Task<ServiceResult> UpdateNotificationPreferenceAsync(string userId, int preference)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult.Fail("找不到用戶");
            }

            // 驗證偏好設定值（1=即時, 2=摘要, 3=僅重要, 4=關閉）
            if (preference < 1 || preference > 4)
            {
                return ServiceResult.Fail("無效的通知偏好設定");
            }

            user.LineNotificationPreference = preference;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                _logger.LogInformation("用戶 {UserId} 已更新通知偏好設定為 {Preference}", userId, preference);
                return ServiceResult.Ok();
            }

            var errorMessages = string.Join(", ", result.Errors.Select(e => e.Description));
            return ServiceResult.Fail(errorMessages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新通知偏好設定時發生錯誤：UserId={UserId}, Preference={Preference}", userId, preference);
            return ServiceResult.Fail("更新通知偏好設定時發生錯誤，請稍後再試");
        }
    }

    public async Task<bool> GetUserLineMessagingApiStatusAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            return user != null && !string.IsNullOrEmpty(user.LineMessagingApiUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得 LINE Messaging API 綁定狀態時發生錯誤：UserId={UserId}", userId);
            return false;
        }
    }
}

