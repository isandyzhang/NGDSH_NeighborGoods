namespace NeighborGoods.Api.Features.Announcements.Contracts;

public sealed record AdminAnnouncementResponse(
    Guid Id,
    string Message,
    byte Severity,
    byte Scope,
    int SortOrder,
    bool IsEnabled,
    DateTime? StartsAt,
    DateTime? EndsAt,
    string? LinkUrl,
    string? LinkLabel,
    DateTime CreatedAt,
    string? CreatedByUserId,
    DateTime? UpdatedAt,
    string? UpdatedByUserId);

public sealed record AdminAnnouncementsListResponse(
    IReadOnlyList<AdminAnnouncementResponse> Items);
