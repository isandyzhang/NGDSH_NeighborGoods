namespace NeighborGoods.Api.Features.Admin.Contracts.Requests;

public sealed record UpsertAdminResidenceRequest(
    string DisplayName,
    string? City,
    string? District,
    string? Notes,
    int SortOrder,
    bool IsActive);

public sealed record UpsertAdminPickupLocationRequest(
    string DisplayName,
    int? ResidenceId,
    int SortOrder,
    bool IsActive);

public sealed record UpsertAdminCategoryRequest(
    string DisplayName,
    int SortOrder,
    bool IsActive);

public sealed record SetLookupEnabledRequest(bool IsEnabled);
