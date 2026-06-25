namespace NeighborGoods.Api.Features.Admin.Contracts.Responses;

public sealed record AdminConversationListItemDto(
    Guid ConversationId,
    Guid ListingId,
    string ListingTitle,
    string Participant1Id,
    string Participant1DisplayName,
    string Participant2Id,
    string Participant2DisplayName,
    string? LastMessagePreview,
    DateTime? LastMessageAt,
    DateTime UpdatedAt,
    int MessageCount);

public sealed record AdminConversationListResponse(
    IReadOnlyList<AdminConversationListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record AdminConversationByListingConversationItemDto(
    Guid ConversationId,
    string Participant1Id,
    string Participant1DisplayName,
    string Participant2Id,
    string Participant2DisplayName,
    int MessageCount,
    DateTime? LastMessageAt
);

public sealed record AdminConversationByListingItemDto(
    Guid ListingId,
    string ListingTitle,
    string SellerDisplayName,
    string? ListingImageUrl,
    int ConversationCount,
    DateTime LastUpdatedAt,
    IReadOnlyList<AdminConversationByListingConversationItemDto> Conversations
);

public sealed record AdminConversationByListingResponse(
    IReadOnlyList<AdminConversationByListingItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
);
