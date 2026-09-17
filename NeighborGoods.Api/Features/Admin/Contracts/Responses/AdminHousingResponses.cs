namespace NeighborGoods.Api.Features.Admin.Contracts.Responses;

public sealed record AdminResidenceResponse(
    int Id,
    string CodeKey,
    string DisplayName,
    string? City,
    string? District,
    string? Notes,
    int SortOrder,
    bool IsActive,
    int ListingCount,
    int PickupLocationCount);

public sealed record AdminPickupLocationResponse(
    int Id,
    string CodeKey,
    string DisplayName,
    int? ResidenceId,
    string? ResidenceName,
    int SortOrder,
    bool IsActive,
    int ListingCount,
    bool IsProtected);

public sealed record AdminCategoryResponse(
    int Id,
    string CodeKey,
    string DisplayName,
    int SortOrder,
    bool IsActive,
    int ListingCount);
