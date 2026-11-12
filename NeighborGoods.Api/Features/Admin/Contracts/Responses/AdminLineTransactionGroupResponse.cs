namespace NeighborGoods.Api.Features.Admin.Contracts.Responses;

public sealed record AdminLineTransactionGroupItemResponse(
    Guid Id,
    string GroupId,
    bool IsActive,
    DateTime JoinedAt,
    DateTime? LeftAt);

public sealed record AdminLineTransactionGroupResponse(
    AdminLineTransactionGroupItemResponse? Active,
    IReadOnlyList<AdminLineTransactionGroupItemResponse> Inactive);
