namespace NeighborGoods.Api.Features.Announcements.Contracts;

public sealed record UpsertAnnouncementRequest(
    string Message,
    byte Severity,
    byte Scope,
    int SortOrder,
    bool IsEnabled,
    DateTime? StartsAt,
    DateTime? EndsAt,
    string? LinkUrl,
    string? LinkLabel);

public sealed record SetAnnouncementEnabledRequest(
    bool IsEnabled);
