using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Account.Contracts.Requests;
using NeighborGoods.Api.Shared.Notifications;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Features.Account.Services;

public sealed class AccountEmailVerificationService(
    NeighborGoodsDbContext dbContext,
    IEmailSender emailSender)
{
    public async Task<(bool Ok, string? ErrorCode, string? ErrorMessage)> SendListingVerificationCodeAsync(
        string userId,
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
        var challenge = new EmailVerificationChallenge
        {
            Id = Guid.NewGuid(),
            Purpose = (byte)EmailVerificationPurpose.ListingEmail,
            NormalizedEmail = normalizedEmail,
            UserId = userId,
            CodeHash = HashCode(code),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(AccountConstants.VerificationCodeExpiresInMinutes),
            ConsumedAt = null
        };

        var oldChallenges = await dbContext.EmailVerificationChallenges
            .Where(x =>
                x.Purpose == (byte)EmailVerificationPurpose.ListingEmail &&
                x.UserId == userId &&
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
                "NeighborGoods 刊登前 Email 驗證碼",
                $"您的 Email 驗證碼為：{code}（{AccountConstants.VerificationCodeExpiresInMinutes} 分鐘內有效）",
                cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message == "EMAIL_NOT_CONFIGURED")
        {
            return (false, "EMAIL_NOT_CONFIGURED", "Email 服務尚未設定。");
        }
        catch
        {
            return (false, "EMAIL_SEND_FAILED", "寄送驗證碼失敗，請稍後再試。");
        }

        return (true, null, null);
    }

    public async Task<(bool Ok, string? ErrorCode, string? ErrorMessage)> VerifyListingEmailCodeAsync(
        string userId,
        VerifyEmailCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        if (normalizedEmail is null || string.IsNullOrWhiteSpace(request.Code))
        {
            return (false, "VALIDATION_ERROR", "Email 或驗證碼格式錯誤。");
        }

        var user = await dbContext.AspNetUsers.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return (false, "USER_NOT_FOUND", "找不到使用者。");
        }

        var challenge = await dbContext.EmailVerificationChallenges
            .Where(x =>
                x.Purpose == (byte)EmailVerificationPurpose.ListingEmail &&
                x.UserId == userId &&
                x.NormalizedEmail == normalizedEmail &&
                x.ConsumedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (challenge is null)
        {
            return (false, "EMAIL_CODE_INVALID", "找不到可用的 Email 驗證碼。");
        }

        if (challenge.ExpiresAt < DateTime.UtcNow)
        {
            return (false, "EMAIL_CODE_EXPIRED", "Email 驗證碼已過期。");
        }

        if (!string.Equals(challenge.CodeHash, HashCode(request.Code), StringComparison.Ordinal))
        {
            return (false, "EMAIL_CODE_INVALID", "Email 驗證碼錯誤。");
        }

        challenge.ConsumedAt = DateTime.UtcNow;
        user.Email = request.Email.Trim();
        user.NormalizedEmail = normalizedEmail;
        user.EmailConfirmed = true;
        user.EmailNotificationEnabled = true;

        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, null, null);
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
