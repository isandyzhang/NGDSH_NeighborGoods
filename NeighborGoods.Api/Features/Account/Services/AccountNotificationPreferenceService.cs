using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Shared.Notifications;
using NeighborGoods.Api.Shared.Persistence;

namespace NeighborGoods.Api.Features.Account.Services;

public sealed class AccountNotificationPreferenceService(NeighborGoodsDbContext dbContext)
{
    public async Task<(bool Ok, string? ErrorCode, string? ErrorMessage)> DisableAllAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.AspNetUsers
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return (false, "USER_NOT_FOUND", "找不到使用者。");
        }

        user.EmailNotificationEnabled = false;
        user.LineNotificationPreference = (int)LineNotificationPreferenceFlags.None;
        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, null, null);
    }

    public async Task<(bool Ok, string? ErrorCode, string? ErrorMessage)> EnableEmailAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.AspNetUsers
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return (false, "USER_NOT_FOUND", "找不到使用者。");
        }

        if (!user.EmailConfirmed)
        {
            return (false, "EMAIL_NOT_VERIFIED", "請先完成 Email 驗證。");
        }

        user.EmailNotificationEnabled = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, null, null);
    }

    public async Task<(bool Ok, string? ErrorCode, string? ErrorMessage)> DisableEmailAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.AspNetUsers
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return (false, "USER_NOT_FOUND", "找不到使用者。");
        }

        user.EmailNotificationEnabled = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, null, null);
    }

    public async Task<(bool Ok, string? ErrorCode, string? ErrorMessage)> DisableLinePreferencesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.AspNetUsers
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return (false, "USER_NOT_FOUND", "找不到使用者。");
        }

        user.LineNotificationPreference = (int)LineNotificationPreferenceFlags.None;
        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, null, null);
    }
}
