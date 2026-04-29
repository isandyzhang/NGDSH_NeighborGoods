using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Account.Contracts.Requests;
using NeighborGoods.Api.Features.Auth.Services;
using NeighborGoods.Api.Shared.Notifications;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Features.Account.Services;

public sealed class AccountRegistrationService(
    NeighborGoodsDbContext dbContext,
    IEmailSender emailSender,
    ITokenService tokenService)
{
    private readonly PasswordHasher<AspNetUser> _passwordHasher = new();

    public async Task<(bool Ok, string? ErrorCode, string? ErrorMessage)> SendRegisterVerificationCodeAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail is null)
        {
            return (false, "VALIDATION_ERROR", "Email 格式不正確。");
        }

        var code = GenerateCode();
        var now = DateTime.UtcNow;
        var lastSentAt = await dbContext.EmailVerificationChallenges
            .Where(x =>
                x.Purpose == (byte)EmailVerificationPurpose.Register &&
                x.NormalizedEmail == normalizedEmail)
            .MaxAsync(x => (DateTime?)x.CreatedAt, cancellationToken);
        if (lastSentAt.HasValue)
        {
            var secondsSinceLastSend = (int)Math.Floor((now - lastSentAt.Value).TotalSeconds);
            if (secondsSinceLastSend < AccountConstants.VerificationCodeResendCooldownSeconds)
            {
                var waitSeconds = AccountConstants.VerificationCodeResendCooldownSeconds - Math.Max(0, secondsSinceLastSend);
                return (false, "EMAIL_CODE_COOLDOWN", $"寄送過於頻繁，請於 {waitSeconds} 秒後再試。");
            }
        }

        var sentCountInLastHour = await dbContext.EmailVerificationChallenges
            .CountAsync(x =>
                x.Purpose == (byte)EmailVerificationPurpose.Register &&
                x.NormalizedEmail == normalizedEmail &&
                x.CreatedAt >= now.AddHours(-1), cancellationToken);
        if (sentCountInLastHour >= AccountConstants.VerificationCodeMaxSendsPerHour)
        {
            return (false, "EMAIL_CODE_RATE_LIMIT", "此 Email 驗證碼發送次數已達每小時上限，請稍後再試。");
        }

        var challenge = new EmailVerificationChallenge
        {
            Id = Guid.NewGuid(),
            Purpose = (byte)EmailVerificationPurpose.Register,
            NormalizedEmail = normalizedEmail,
            UserId = null,
            CodeHash = HashCode(code),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(AccountConstants.VerificationCodeExpiresInMinutes),
            ConsumedAt = null
        };

        var oldChallenges = await dbContext.EmailVerificationChallenges
            .Where(x =>
                x.Purpose == (byte)EmailVerificationPurpose.Register &&
                x.NormalizedEmail == normalizedEmail &&
                x.ConsumedAt == null)
            .ToListAsync(cancellationToken);
        if (oldChallenges.Count > 0)
        {
            dbContext.EmailVerificationChallenges.RemoveRange(oldChallenges);
        }

        dbContext.EmailVerificationChallenges.Add(challenge);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await emailSender.SendAsync(
                email.Trim(),
                "NeighborGoods 註冊驗證碼",
                $"您的註冊驗證碼為：{code}（{AccountConstants.VerificationCodeExpiresInMinutes} 分鐘內有效）",
                cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message == "EMAIL_NOT_CONFIGURED")
        {
            dbContext.EmailVerificationChallenges.Remove(challenge);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (false, "EMAIL_NOT_CONFIGURED", "Email 服務尚未設定。");
        }
        catch
        {
            dbContext.EmailVerificationChallenges.Remove(challenge);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (false, "EMAIL_SEND_FAILED", "寄送驗證碼失敗，請稍後再試。");
        }

        return (true, null, null);
    }

    public async Task<(AuthTokenPair? Tokens, string? ErrorCode, string? ErrorMessage)> RegisterAsync(
        RegisterAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var userName = request.UserName.Trim();
        var displayName = request.DisplayName.Trim();
        var password = request.Password;
        var code = request.EmailVerificationCode.Trim();
        var normalizedEmail = NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(userName) ||
            string.IsNullOrWhiteSpace(displayName) ||
            string.IsNullOrWhiteSpace(password) ||
            normalizedEmail is null)
        {
            return (null, "VALIDATION_ERROR", "註冊資料不完整或格式不正確。");
        }

        if (displayName.Length > AccountConstants.MaxDisplayNameLength)
        {
            return (null, "VALIDATION_ERROR", $"顯示名稱不可超過 {AccountConstants.MaxDisplayNameLength} 字元。");
        }

        if (code.Length != AccountConstants.VerificationCodeLength)
        {
            return (null, "EMAIL_CODE_INVALID", "Email 驗證碼格式錯誤。");
        }

        var normalizedUserName = userName.ToUpperInvariant();
        var userNameExists = await dbContext.AspNetUsers
            .AnyAsync(x => x.NormalizedUserName == normalizedUserName, cancellationToken);
        if (userNameExists)
        {
            return (null, "REGISTER_USERNAME_TAKEN", "帳號名稱已被使用。");
        }

        var emailExists = await dbContext.AspNetUsers
            .AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            return (null, "REGISTER_EMAIL_TAKEN", "Email 已被使用。");
        }

        var challenge = await dbContext.EmailVerificationChallenges
            .Where(x =>
                x.Purpose == (byte)EmailVerificationPurpose.Register &&
                x.NormalizedEmail == normalizedEmail &&
                x.UserId == null &&
                x.ConsumedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (challenge is null)
        {
            return (null, "EMAIL_CODE_INVALID", "找不到可用的 Email 驗證碼。");
        }

        if (challenge.ExpiresAt < DateTime.UtcNow)
        {
            return (null, "EMAIL_CODE_EXPIRED", "Email 驗證碼已過期。");
        }

        if (!string.Equals(challenge.CodeHash, HashCode(code), StringComparison.Ordinal))
        {
            return (null, "EMAIL_CODE_INVALID", "Email 驗證碼錯誤。");
        }

        var email = request.Email.Trim();
        var user = new AspNetUser
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = userName,
            NormalizedUserName = normalizedUserName,
            DisplayName = displayName,
            Email = email,
            NormalizedEmail = normalizedEmail,
            EmailConfirmed = true,
            Role = 0,
            CreatedAt = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            PhoneNumberConfirmed = false,
            TwoFactorEnabled = false,
            LockoutEnabled = false,
            AccessFailedCount = 0,
            LineNotificationPreference = 0,
            EmailNotificationEnabled = false,
            TopPinCredits = 0
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        challenge.ConsumedAt = DateTime.UtcNow;
        dbContext.AspNetUsers.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        var tokens = await tokenService.IssueAsync(user, cancellationToken);
        return (tokens, null, null);
    }

    private static string? NormalizeEmail(string email)
    {
        var value = email.Trim();
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('@') || !value.Contains('.'))
        {
            return null;
        }

        return value.ToUpperInvariant();
    }

    private static string GenerateCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static string HashCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim())));
}
