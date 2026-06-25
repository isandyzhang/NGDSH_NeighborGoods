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
    int SoldListings,
    int DonatedListings,
    int GivenOrTradedListings,
    int ActiveListingsLast7Days,
    int SoldListingsLast7Days,
    int DonatedListingsLast7Days,
    int GivenOrTradedListingsLast7Days,
    int TotalMembers,
    int PasswordLoginMembers,
    int LineLoginMembers,
    int EmailBoundMembers,
    int LineOfficialBoundMembers,
    int ActiveMembers24h,
    int ActiveMembers7d,
    int ActiveMembers30d,
    int EmailedMembers24h,
    int EmailedMembers7d,
    int EmailedMembers30d,
    int LineNotifiedMembers24h,
    int LineNotifiedMembers7d,
    int LineNotifiedMembers30d,
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
