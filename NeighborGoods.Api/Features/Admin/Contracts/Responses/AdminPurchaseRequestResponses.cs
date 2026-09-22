namespace NeighborGoods.Api.Features.Admin.Contracts.Responses;

public sealed record AdminListingPurchaseRequestsResponse(
    Guid ListingId,
    IReadOnlyList<AdminPurchaseRequestResponse> Items);

public sealed record AdminPurchaseRequestResponse(
    Guid Id,
    Guid ListingId,
    Guid ConversationId,
    string BuyerId,
    string BuyerDisplayName,
    string SellerId,
    string SellerDisplayName,
    int Status,
    DateTime CreatedAt,
    DateTime ExpireAt,
    DateTime? RespondedAt,
    string? ResponseReason,
    bool IsCurrent);
