namespace NeighborGoods.Notifications;

public sealed record LineMyListingsSummary(
    int Total,
    int Active,
    int Reserved,
    int Sold);

public sealed record LineMyMessagesSummary(
    int ConversationCount,
    int UnreadCount,
    string UserDisplayName,
    DateTime? RegisteredAt,
    IReadOnlyList<LineRecentConversationItem> RecentConversations);

public sealed record LineMyListingCardItem(
    Guid ListingId,
    string Title,
    string Description,
    string? ImageUrl,
    int Status,
    string ResidenceName,
    string PickupLocationName,
    bool IsFree,
    decimal Price,
    int FavoriteCount,
    DateTime LastStatusChangedAt,
    int UnreadCount,
    DateTime UpdatedAt,
    DateTime CreatedAt);

public sealed record LineListingExpiryItem(
    Guid ListingId,
    string Title,
    string? ImageUrl,
    decimal Price,
    bool IsFree,
    string CategoryName);

public sealed record LineRecentConversationItem(
    Guid ConversationId,
    string OtherDisplayName,
    string ListingTitle,
    string LatestMessageContent,
    DateTime LatestMessageAt,
    int UnreadCount);
