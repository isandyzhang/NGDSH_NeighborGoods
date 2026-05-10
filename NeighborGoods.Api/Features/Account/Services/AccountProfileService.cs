using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Account.Contracts.Responses;
using NeighborGoods.Api.Features.Listing;
using NeighborGoods.Api.Shared.Persistence;

namespace NeighborGoods.Api.Features.Account.Services;

public sealed class AccountProfileService(NeighborGoodsDbContext dbContext)
{
    private static readonly char[] LineContactAllowedSpecialChars = ['.', '-', '_'];

    public async Task<(AccountMeResponse? Data, string? ErrorCode, string? ErrorMessage)> GetMeAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.AspNetUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return (null, "USER_NOT_FOUND", "找不到使用者。");
        }

        var totalListings = await dbContext.Listings
            .CountAsync(l => l.SellerId == userId, cancellationToken);
        var activeListings = await dbContext.Listings
            .CountAsync(l => l.SellerId == userId && l.Status == (int)ListingStatus.Active, cancellationToken);
        var completedListings = await dbContext.Listings
            .CountAsync(l =>
                l.SellerId == userId &&
                (l.Status == (int)ListingStatus.Sold ||
                 l.Status == (int)ListingStatus.Donated ||
                 l.Status == (int)ListingStatus.GivenOrTraded),
                cancellationToken);

        var data = new AccountMeResponse(
            user.Id,
            user.UserName ?? string.Empty,
            user.DisplayName,
            user.Role,
            user.Email,
            user.EmailConfirmed,
            user.EmailNotificationEnabled,
            user.LineContactId,
            user.LineUserId,
            !string.IsNullOrWhiteSpace(user.LineMessagingApiUserId),
            user.CreatedAt,
            new AccountStatisticsResponse(
                totalListings,
                activeListings,
                completedListings,
                user.TopPinCredits));

        return (data, null, null);
    }

    public async Task<(bool Ok, string? ErrorCode, string? ErrorMessage)> UpdateProfileAsync(
        string userId,
        string? displayName,
        string? lineContactId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.AspNetUsers
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return (false, "USER_NOT_FOUND", "找不到使用者。");
        }

        if (displayName is not null)
        {
            var value = displayName.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return (false, "VALIDATION_ERROR", "顯示名稱不可為空白。");
            }

            if (value.Length > AccountConstants.MaxDisplayNameLength)
            {
                return (false, "VALIDATION_ERROR", $"顯示名稱不可超過 {AccountConstants.MaxDisplayNameLength} 字元。");
            }

            user.DisplayName = value;
        }

        if (lineContactId is not null)
        {
            var normalizedLineContactId = lineContactId.Trim();
            if (normalizedLineContactId.Length == 0)
            {
                user.LineContactId = null;
            }
            else
            {
                if (normalizedLineContactId.Length is < 4 or > 32)
                {
                    return (false, "VALIDATION_ERROR", "LINE ID 長度需介於 4 到 32 字元。");
                }

                var isValidLineContactId = normalizedLineContactId.All(char.IsLetterOrDigit) ||
                    normalizedLineContactId.All(ch => char.IsLetterOrDigit(ch) || LineContactAllowedSpecialChars.Contains(ch));
                if (!isValidLineContactId)
                {
                    return (false, "VALIDATION_ERROR", "LINE ID 僅允許英數與 . - _ 符號。");
                }

                user.LineContactId = normalizedLineContactId;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, null, null);
    }
}
