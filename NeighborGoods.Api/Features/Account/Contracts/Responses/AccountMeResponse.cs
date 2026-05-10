namespace NeighborGoods.Api.Features.Account.Contracts.Responses;

public sealed record AccountMeResponse(
    string UserId,
    string UserName,
    string DisplayName,
    int Role,
    string? Email,
    bool EmailConfirmed,
    bool EmailNotificationEnabled,
    string? LineContactId,
    string? LineUserId,
    bool LineNotifyBound,
    DateTime CreatedAt,
    AccountStatisticsResponse Statistics
);

public sealed record AccountStatisticsResponse(
    int TotalListings,
    int ActiveListings,
    int CompletedListings,
    int TopPinCredits
);
