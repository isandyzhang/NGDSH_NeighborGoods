namespace NeighborGoods.Api.Features.Admin.Contracts.Responses;

public sealed record AdminMemberListItemResponse(
    string Id,
    string DisplayName,
    string? UserName,
    string? Email,
    bool EmailConfirmed,
    string? LineUserId,
    string? LineContactId,
    int Role,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    DateTime? LineMessagingApiAuthorizedAt,
    int LineNotificationPreference,
    int TopPinCredits,
    bool IsQuickResponder,
    string? PhoneNumber,
    bool LockoutEnabled,
    bool HasPassword
);

public sealed record AdminMemberListResponse(
    IReadOnlyList<AdminMemberListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
);
