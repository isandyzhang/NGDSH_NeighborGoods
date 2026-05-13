using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeighborGoods.Api.Features.Account.Contracts.Responses;
using NeighborGoods.Api.Features.Integrations.Line.Services;
using NeighborGoods.Api.Shared.Notifications;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Features.Account.Services;

public sealed class AccountLineBindingService(
    NeighborGoodsDbContext dbContext,
    ILineMessageSender lineMessageSender,
    LineFlexMessageBuilder flexMessageBuilder,
    IOptions<LineMessagingOptions> lineMessagingOptions,
    ILineLiffIdTokenVerifier liffIdTokenVerifier)
{
    private static readonly TimeSpan BindingTokenTtl = TimeSpan.FromMinutes(15);

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

        if (string.IsNullOrWhiteSpace(_options.LiffId))
        {
            return (null, "LINE_BIND_LIFF_NOT_CONFIGURED", "LIFF 尚未設定，請管理員設定 LineMessagingApi:LiffId。");
        }

        var now = DateTime.UtcNow;
        var existingPendings = await dbContext.LineBindingPendings
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var activePending = existingPendings
            .FirstOrDefault(x => x.CreatedAt + BindingTokenTtl >= now);

        if (activePending is not null)
        {
            // 同一使用者在有效期間重複點擊「開始綁定」時，重用現有 token，避免舊連結瞬間失效。
            var existingBotId = string.IsNullOrWhiteSpace(_options.BotId) ? "@559fslxw" : _options.BotId.Trim();
            if (!existingBotId.StartsWith("@", StringComparison.Ordinal))
            {
                existingBotId = "@" + existingBotId;
            }

            var existingBotLink = $"line://ti/p/{existingBotId}";
            var existingLiffId = _options.LiffId.Trim();
            var existingTarget =
                $"/liff/line-notify?bindToken={Uri.EscapeDataString(activePending.Token)}&botLink={Uri.EscapeDataString(existingBotLink)}";
            var existingLiffUrl =
                $"https://liff.line.me/{existingLiffId}/liff-entry?target={Uri.EscapeDataString(existingTarget)}";

            var staleIds = existingPendings
                .Where(x => x.Id != activePending.Id)
                .Select(x => x.Id)
                .ToList();
            if (staleIds.Count > 0)
            {
                await dbContext.LineBindingPendings
                    .Where(x => staleIds.Contains(x.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            return (new StartLineBindingResponse(activePending.Id, existingLiffUrl, activePending.Token, existingBotLink), null, null);
        }

        if (existingPendings.Count > 0)
        {
            await dbContext.LineBindingPendings
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        var token = Guid.NewGuid().ToString("N");
        var pending = new LineBindingPending
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            LineUserId = null,
            CreatedAt = now
        };

        dbContext.LineBindingPendings.Add(pending);
        await dbContext.SaveChangesAsync(cancellationToken);

        var botId = string.IsNullOrWhiteSpace(_options.BotId) ? "@559fslxw" : _options.BotId.Trim();
        if (!botId.StartsWith("@", StringComparison.Ordinal))
        {
            botId = "@" + botId;
        }

        var botLink = $"line://ti/p/{botId}";
        var liffId = _options.LiffId.Trim();
        var target = $"/liff/line-notify?bindToken={Uri.EscapeDataString(token)}&botLink={Uri.EscapeDataString(botLink)}";
        var liffUrl =
            $"https://liff.line.me/{liffId}/liff-entry?target={Uri.EscapeDataString(target)}";

        return (new StartLineBindingResponse(pending.Id, liffUrl, token, botLink), null, null);
    }

    public async Task<(bool Ok, string? ErrorCode, string? ErrorMessage)> CompleteLiffBindingAsync(
        string bindingToken,
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bindingToken))
        {
            return (false, "LINE_BIND_TOKEN_MISSING", "缺少綁定憑證。");
        }

        var trimmedToken = bindingToken.Trim();
        var pending = await dbContext.LineBindingPendings
            .FirstOrDefaultAsync(x => x.Token == trimmedToken, cancellationToken);
        if (pending is null)
        {
            return (false, "LINE_BIND_PENDING_NOT_FOUND", "綁定連結無效或已使用，請回網站重新開始。");
        }

        if (pending.CreatedAt + BindingTokenTtl < DateTime.UtcNow)
        {
            dbContext.LineBindingPendings.Remove(pending);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (false, "LINE_BIND_TOKEN_EXPIRED", "綁定連結已過期，請回網站重新開始。");
        }

        var (sub, verifyCode, verifyMessage) = await liffIdTokenVerifier.VerifyAsync(idToken, cancellationToken);
        if (sub is null)
        {
            return (false, verifyCode!, verifyMessage!);
        }

        var user = await dbContext.AspNetUsers.FirstOrDefaultAsync(x => x.Id == pending.UserId, cancellationToken);
        if (user is null)
        {
            return (false, "USER_NOT_FOUND", "找不到使用者。");
        }

        if (!string.IsNullOrWhiteSpace(user.LineMessagingApiUserId))
        {
            dbContext.LineBindingPendings.Remove(pending);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (false, "LINE_BIND_ALREADY_BOUND", "此帳號已綁定 LINE 通知。");
        }

        var lineUserIdExists = await dbContext.AspNetUsers
            .AnyAsync(x => x.Id != user.Id && x.LineMessagingApiUserId == sub, cancellationToken);
        if (lineUserIdExists)
        {
            dbContext.LineBindingPendings.Remove(pending);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (false, "LINE_BIND_LINE_USER_ALREADY_USED", "此 LINE 帳號已被其他用戶綁定。");
        }

        user.LineMessagingApiUserId = sub;
        user.LineMessagingApiAuthorizedAt = DateTime.UtcNow;
        dbContext.LineBindingPendings.Remove(pending);
        await dbContext.SaveChangesAsync(cancellationToken);

        await SendFlexNoticeAsync(
            user.LineMessagingApiUserId,
            "LINE 綁定成功",
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
            await SendFlexNoticeAsync(
                lineUserId,
                "歡迎回來",
                "歡迎回來！您已經綁定 LINE 通知功能。",
                cancellationToken);
            return;
        }

        await SendFlexNoticeAsync(
            lineUserId,
            "LINE 通知綁定",
            "請至 NeighborGoods 網站「我的帳號」開啟 LINE 官方通知綁定，依畫面於 LINE 內完成即可收到訊息通知。",
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

    private async Task SendFlexNoticeAsync(
        string lineUserId,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        var card = flexMessageBuilder.BuildNoticeCard(title, message);
        await lineMessageSender.PushFlexAsync(lineUserId, card.AltText, card.Contents, cancellationToken);
    }
}
