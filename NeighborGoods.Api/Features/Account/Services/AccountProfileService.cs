using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Account.Contracts.Responses;
using NeighborGoods.Api.Features.Listing;
using NeighborGoods.Api.Shared.Persistence;

namespace NeighborGoods.Api.Features.Account.Services;

public sealed class AccountProfileService(NeighborGoodsDbContext dbContext)
{
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
            user.Email,
            user.EmailConfirmed,
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

    public async Task<(bool Ok, string? ErrorCode, string? ErrorMessage)> UpdateDisplayNameAsync(
        string userId,
        string displayName,
        CancellationToken cancellationToken = default)
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

        var user = await dbContext.AspNetUsers
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return (false, "USER_NOT_FOUND", "找不到使用者。");
        }

        user.DisplayName = value;
        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, null, null);
    }
}
