using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeighborGoods.Api.Features.Account.Contracts.Responses;
using NeighborGoods.Api.Shared.Notifications;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Features.Account.Services;

public sealed class AccountLineBindingService(
    NeighborGoodsDbContext dbContext,
    ILineMessageSender lineMessageSender,
    IOptions<LineMessagingOptions> lineMessagingOptions)
{
    private readonly LineMessagingOptions _options = lineMessagingOptions.Value;

    public async Task<(StartLineBindingResponse? Data, string? ErrorCode, string? ErrorMessage)> StartAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.AspNetUsers.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return (null, "USER_NOT_FOUND", "找不到使用者。");
        }

        if (!string.IsNullOrWhiteSpace(user.LineMessagingApiUserId))
        {
            return (null, "LINE_BIND_ALREADY_BOUND", "您已經綁定 LINE 通知功能。");
        }

        var token = Guid.NewGuid().ToString("N");
        var pending = new LineBindingPending
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            LineUserId = null,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.LineBindingPendings.Add(pending);
        await dbContext.SaveChangesAsync(cancellationToken);

        var botId = string.IsNullOrWhiteSpace(_options.BotId) ? "@559fslxw" : _options.BotId.Trim();
        if (!botId.StartsWith("@", StringComparison.Ordinal))
        {
            botId = "@" + botId;
        }

        var botLink = $"line://ti/p/{botId}";
        var qrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=300x300&data={Uri.EscapeDataString(botLink)}";

        return (new StartLineBindingResponse(pending.Id, botLink, qrCodeUrl), null, null);
    }

    public async Task<(LineBindingStatusResponse? Data, string? ErrorCode, string? ErrorMessage)> GetStatusAsync(
        string userId,
        Guid pendingBindingId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.AspNetUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return (null, "USER_NOT_FOUND", "找不到使用者。");
        }

        if (!string.IsNullOrWhiteSpace(user.LineMessagingApiUserId))
        {
            return (new LineBindingStatusResponse("completed", "您已經綁定 LINE 通知功能"), null, null);
        }

        var pending = await dbContext.LineBindingPendings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == pendingBindingId && x.UserId == userId, cancellationToken);
        if (pending is null)
        {
            return (new LineBindingStatusResponse("not_found", "找不到綁定記錄，請重新開始"), null, null);
        }

        if (!string.IsNullOrWhiteSpace(pending.LineUserId))
        {
            return (new LineBindingStatusResponse("ready", "已加入 Bot，請點擊確認綁定", pending.LineUserId), null, null);
        }

        return (new LineBindingStatusResponse("waiting", "正在等待加入 Bot..."), null, null);
    }

    public async Task<(bool Ok, string? ErrorCode, string? ErrorMessage)> ConfirmAsync(
        string userId,
        Guid pendingBindingId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.AspNetUsers.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return (false, "USER_NOT_FOUND", "找不到使用者。");
        }

        var pending = await dbContext.LineBindingPendings
            .FirstOrDefaultAsync(x => x.Id == pendingBindingId && x.UserId == userId, cancellationToken);
        if (pending is null)
        {
            return (false, "LINE_BIND_PENDING_NOT_FOUND", "找不到綁定記錄。");
        }

        if (string.IsNullOrWhiteSpace(pending.LineUserId))
        {
            return (false, "LINE_BIND_LINE_USER_MISSING", "尚未收到 LINE follow 事件，請先加入 Bot。");
        }

        var lineUserIdExists = await dbContext.AspNetUsers
            .AnyAsync(x => x.Id != userId && x.LineMessagingApiUserId == pending.LineUserId, cancellationToken);
        if (lineUserIdExists)
        {
            dbContext.LineBindingPendings.Remove(pending);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (false, "LINE_BIND_LINE_USER_ALREADY_USED", "此 LINE 帳號已被其他用戶綁定。");
        }

        user.LineMessagingApiUserId = pending.LineUserId;
        user.LineMessagingApiAuthorizedAt = DateTime.UtcNow;
        dbContext.LineBindingPendings.Remove(pending);
        await dbContext.SaveChangesAsync(cancellationToken);

        await lineMessageSender.SendTextAsync(
            user.LineMessagingApiUserId,
            "歡迎使用 LINE 通知功能！您現在可以透過 LINE 接收訊息通知。",
            cancellationToken);

        return (true, null, null);
    }

    public async Task<(bool Ok, string? ErrorCode, string? ErrorMessage)> UnbindAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.AspNetUsers.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return (false, "USER_NOT_FOUND", "找不到使用者。");
        }

        user.LineMessagingApiUserId = null;
        user.LineMessagingApiAuthorizedAt = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        return (true, null, null);
    }

    public async Task HandleFollowAsync(string lineUserId, CancellationToken cancellationToken = default)
    {
        var existingUser = await dbContext.AspNetUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.LineMessagingApiUserId == lineUserId, cancellationToken);
        if (existingUser is not null)
        {
            await lineMessageSender.SendTextAsync(
                lineUserId,
                "歡迎回來！您已經綁定 LINE 通知功能。",
                cancellationToken);
            return;
        }

        var pendingBindings = await dbContext.LineBindingPendings
            .Where(x => x.LineUserId == null)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        if (pendingBindings.Count == 1)
        {
            var pending = pendingBindings[0];
            pending.LineUserId = lineUserId;
            await dbContext.SaveChangesAsync(cancellationToken);
            await lineMessageSender.SendTextAsync(
                lineUserId,
                "歡迎加入！請返回網站點擊「確認綁定」按鈕完成綁定。",
                cancellationToken);
            return;
        }

        if (pendingBindings.Count > 1)
        {
            await lineMessageSender.SendTextAsync(
                lineUserId,
                "歡迎加入！請返回網站完成 LINE 通知綁定。",
                cancellationToken);
            return;
        }

        await lineMessageSender.SendTextAsync(
            lineUserId,
            "歡迎加入！請前往網站個人資料頁面完成 LINE 通知綁定。",
            cancellationToken);
    }

    public async Task HandleUnfollowAsync(string lineUserId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.AspNetUsers
            .FirstOrDefaultAsync(x => x.LineMessagingApiUserId == lineUserId, cancellationToken);
        if (user is null)
        {
            return;
        }

        user.LineMessagingApiUserId = null;
        user.LineMessagingApiAuthorizedAt = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
