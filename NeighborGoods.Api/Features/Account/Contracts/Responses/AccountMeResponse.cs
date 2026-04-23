namespace NeighborGoods.Api.Features.Account.Contracts.Responses;

public sealed record AccountMeResponse(
    string UserId,
    string UserName,
    string DisplayName,
    string? Email,
    bool EmailConfirmed,
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
