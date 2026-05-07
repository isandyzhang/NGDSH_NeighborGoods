namespace NeighborGoods.Api.Features.Admin.Contracts.Responses;

public sealed record AdminDashboardResponse(
    AdminDashboardKpiResponse Kpi,
    IReadOnlyList<AdminDashboardListingResponse> LatestListings,
    IReadOnlyList<AdminDashboardMessageResponse> LatestMessages,
    IReadOnlyList<AdminDashboardTopSubmissionResponse> LatestTopSubmissions
);

public sealed record AdminDashboardKpiResponse(
    int TotalListings,
    int ActiveListings,
    int CompletedListings,
    int PendingTopSubmissions,
    int UnreadAdminMessages
);

public sealed record AdminDashboardListingResponse(
    Guid Id,
    string Title,
    string SellerDisplayName,
    decimal Price,
    bool IsFree,
    int Status,
    bool IsPinned,
    DateTime CreatedAt
);

public sealed record AdminDashboardMessageResponse(
    Guid Id,
    string SenderDisplayName,
    string Content,
    bool IsRead,
    DateTime CreatedAt
);

public sealed record AdminDashboardTopSubmissionResponse(
    int Id,
    string UserDisplayName,
    Guid? ListingId,
    string FeedbackTitle,
    int Status,
    DateTime CreatedAt
);
