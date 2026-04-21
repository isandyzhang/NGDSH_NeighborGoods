using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Account.Contracts.Requests;
using NeighborGoods.Api.Features.Account.Contracts.Responses;
using NeighborGoods.Api.Shared.Notifications;
using NeighborGoods.Api.Shared.Persistence;

namespace NeighborGoods.Api.Features.Account.Services;

public sealed class AccountLinePreferenceService(NeighborGoodsDbContext dbContext)
{
    public async Task<(LinePreferencesResponse? Data, string? ErrorCode, string? ErrorMessage)> GetAsync(
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

        return (ToResponse(user.LineNotificationPreference, user.LineNotificationLastSentAt), null, null);
    }

    public async Task<(LinePreferencesResponse? Data, string? ErrorCode, string? ErrorMessage)> UpdateAsync(
        string userId,
        UpdateLinePreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.AspNetUsers
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return (null, "USER_NOT_FOUND", "找不到使用者。");
        }

        var next = LineNotificationPreferenceFlags.None;
        if (request.MarketingPushEnabled)
        {
            if (request.PreferenceNewListings)
            {
                next |= LineNotificationPreferenceFlags.NewListings;
            }

            if (request.PreferencePriceDrop)
            {
                next |= LineNotificationPreferenceFlags.PriceDrop;
            }

            if (request.PreferenceMessageDigest)
            {
                next |= LineNotificationPreferenceFlags.MessageDigest;
            }
        }

        user.LineNotificationPreference = (int)next;
        await dbContext.SaveChangesAsync(cancellationToken);

        return (ToResponse(user.LineNotificationPreference, user.LineNotificationLastSentAt), null, null);
    }

    private static LinePreferencesResponse ToResponse(int preferenceValue, DateTime? lastSentAt)
    {
        var preference = (LineNotificationPreferenceFlags)preferenceValue;
        return new LinePreferencesResponse(
            MarketingPushEnabled: preference != LineNotificationPreferenceFlags.None,
            PreferenceNewListings: preference.HasFlag(LineNotificationPreferenceFlags.NewListings),
            PreferencePriceDrop: preference.HasFlag(LineNotificationPreferenceFlags.PriceDrop),
            PreferenceMessageDigest: preference.HasFlag(LineNotificationPreferenceFlags.MessageDigest),
            LastPreferencePushSentAt: lastSentAt);
    }
}
