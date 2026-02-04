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
                Email = model.Email.Trim(),
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

    public async Task<ApplicationUser?> GetUserByLineMessagingApiUserIdAsync(string lineUserId)
    {
        try
        {
            return await _db.Users
                .FirstOrDefaultAsync(u => u.LineMessagingApiUserId == lineUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根據 LINE Messaging API User ID 查詢用戶時發生錯誤：LineUserId={LineUserId}", lineUserId);
            return null;
        }
    }

    public async Task<Models.Entities.LineBindingPending?> GetLineBindingPendingByUserIdAsync(string userId, Guid? pendingBindingId)
    {
        try
        {
            if (pendingBindingId.HasValue)
            {
                return await _db.LineBindingPending
                    .FirstOrDefaultAsync(p => p.Id == pendingBindingId.Value && p.UserId == userId);
            }
            else
            {
                // 如果沒有提供 ID，查詢該用戶最新的暫存記錄
                return await _db.LineBindingPending
                    .Where(p => p.UserId == userId)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得綁定暫存記錄時發生錯誤：UserId={UserId}, PendingBindingId={PendingBindingId}", userId, pendingBindingId);
            return null;
        }
    }

    public async Task<List<Models.Entities.LineBindingPending>> GetLineBindingPendingByLineUserIdAsync(string? lineUserId)
    {
        try
        {
            if (string.IsNullOrEmpty(lineUserId))
            {
                // 查詢所有 LineUserId 為 null 的記錄（正在綁定中的記錄）
                return await _db.LineBindingPending
                    .Where(p => p.LineUserId == null)
                    .ToListAsync();
            }
            else
            {
                // 根據 LineUserId 查詢
                return await _db.LineBindingPending
                    .Where(p => p.LineUserId == lineUserId)
                    .ToListAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根據 LINE User ID 查詢暫存記錄時發生錯誤：LineUserId={LineUserId}", lineUserId);
            return new List<Models.Entities.LineBindingPending>();
        }
    }

    public async Task<ServiceResult<Models.Entities.LineBindingPending>> CreateLineBindingPendingAsync(string userId, string token)
    {
        try
        {
            var pendingBinding = new Models.Entities.LineBindingPending
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = token,
                LineUserId = null,
                CreatedAt = TaiwanTime.Now
            };

            _db.LineBindingPending.Add(pendingBinding);
            await _db.SaveChangesAsync();

            _logger.LogInformation("建立綁定暫存記錄：UserId={UserId}, Token={Token}", userId, token);
            return ServiceResult<Models.Entities.LineBindingPending>.Ok(pendingBinding);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "建立綁定暫存記錄時發生錯誤：UserId={UserId}, Token={Token}", userId, token);
            return ServiceResult<Models.Entities.LineBindingPending>.Fail("建立綁定暫存記錄時發生錯誤，請稍後再試");
        }
    }

    public async Task<ServiceResult> UpdateLineBindingPendingLineUserIdAsync(Guid pendingId, string lineUserId)
    {
        try
        {
            var pending = await _db.LineBindingPending
                .FirstOrDefaultAsync(p => p.Id == pendingId);

            if (pending == null)
            {
                return ServiceResult.Fail("找不到綁定暫存記錄");
            }

            pending.LineUserId = lineUserId;
            await _db.SaveChangesAsync();

            _logger.LogInformation("更新綁定暫存記錄的 LINE User ID：PendingId={PendingId}, LineUserId={LineUserId}", pendingId, lineUserId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新綁定暫存記錄的 LINE User ID 時發生錯誤：PendingId={PendingId}, LineUserId={LineUserId}", pendingId, lineUserId);
            return ServiceResult.Fail("更新綁定暫存記錄時發生錯誤，請稍後再試");
        }
    }

    public async Task<ServiceResult> DeleteLineBindingPendingAsync(Guid pendingId)
    {
        try
        {
            var pending = await _db.LineBindingPending
                .FirstOrDefaultAsync(p => p.Id == pendingId);

            if (pending == null)
            {
                return ServiceResult.Fail("找不到綁定暫存記錄");
            }

            _db.LineBindingPending.Remove(pending);
            await _db.SaveChangesAsync();

            _logger.LogInformation("刪除綁定暫存記錄：PendingId={PendingId}", pendingId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除綁定暫存記錄時發生錯誤：PendingId={PendingId}", pendingId);
            return ServiceResult.Fail("刪除綁定暫存記錄時發生錯誤，請稍後再試");
        }
    }

    public async Task<bool> CheckLineUserIdExistsAsync(string lineUserId, string excludeUserId)
    {
        try
        {
            return await _db.Users
                .AnyAsync(u => u.LineMessagingApiUserId == lineUserId && u.Id != excludeUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "檢查 LINE User ID 是否存在時發生錯誤：LineUserId={LineUserId}, ExcludeUserId={ExcludeUserId}", lineUserId, excludeUserId);
            return false;
        }
    }

    public async Task<ServiceResult> EnableEmailNotificationAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult.Fail("找不到用戶");
            }

            // 檢查 Email 是否已驗證
            if (string.IsNullOrEmpty(user.Email) || !await _userManager.IsEmailConfirmedAsync(user))
            {
                return ServiceResult.Fail("請先設定並驗證您的 Email 地址");
            }

            user.EmailNotificationEnabled = true;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                _logger.LogInformation("用戶 {UserId} 已啟用 Email 通知", userId);
                return ServiceResult.Ok();
            }

            var errorMessages = string.Join(", ", result.Errors.Select(e => e.Description));
            return ServiceResult.Fail(errorMessages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "啟用 Email 通知時發生錯誤：UserId={UserId}", userId);
            return ServiceResult.Fail("啟用 Email 通知時發生錯誤，請稍後再試");
        }
    }

    public async Task<ServiceResult> DisableEmailNotificationAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult.Fail("找不到用戶");
            }

            // 移除 Email 並關閉通知，之後若要再次使用需重新驗證
            user.EmailNotificationEnabled = false;
            user.Email = null;
            user.EmailConfirmed = false;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                _logger.LogInformation("用戶 {UserId} 已移除 Email 並停用 Email 通知", userId);
                return ServiceResult.Ok();
            }

            var errorMessages = string.Join(", ", result.Errors.Select(e => e.Description));
            return ServiceResult.Fail(errorMessages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停用 Email 通知時發生錯誤：UserId={UserId}", userId);
            return ServiceResult.Fail("停用 Email 通知時發生錯誤，請稍後再試");
        }
    }

    public async Task<bool> GetUserEmailNotificationStatusAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            return user != null && user.EmailNotificationEnabled && 
                   !string.IsNullOrEmpty(user.Email) && 
                   await _userManager.IsEmailConfirmedAsync(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得 Email 通知狀態時發生錯誤：UserId={UserId}", userId);
            return false;
        }
    }

    public async Task<ServiceResult> SetEmailAndEnableNotificationAsync(string userId, string email)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult.Fail("找不到用戶");
            }

            // 設定 Email（不需要驗證）
            user.Email = email;
            user.EmailConfirmed = true; // 直接標記為已驗證
            user.EmailNotificationEnabled = true;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                _logger.LogInformation("用戶 {UserId} 已設定 Email 並啟用通知：Email={Email}", userId, email);
                return ServiceResult.Ok();
            }

            var errorMessages = string.Join(", ", result.Errors.Select(e => e.Description));
            return ServiceResult.Fail(errorMessages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "設定 Email 並啟用通知時發生錯誤：UserId={UserId}, Email={Email}", userId, email);
            return ServiceResult.Fail("設定 Email 時發生錯誤，請稍後再試");
        }
    }
}

