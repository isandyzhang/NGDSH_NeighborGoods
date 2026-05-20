namespace NeighborGoods.Api.Features.Announcements.Contracts;

public sealed record ActiveAnnouncementItemResponse(
    Guid Id,
    string Message,
    byte Severity,
    byte Scope,
    string? LinkUrl,
    string? LinkLabel);

public sealed record ActiveAnnouncementsResponse(
    IReadOnlyList<ActiveAnnouncementItemResponse> Items);
