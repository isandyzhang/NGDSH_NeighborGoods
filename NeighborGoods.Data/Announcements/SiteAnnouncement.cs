namespace NeighborGoods.Data.Announcements;

public sealed class SiteAnnouncement
{
    public Guid Id { get; set; }

    public string Message { get; set; } = string.Empty;

    public byte Severity { get; set; } = (byte)AnnouncementSeverity.Info;

    public byte Scope { get; set; } = (byte)AnnouncementScope.Global;

    public int SortOrder { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime? StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public string? LinkUrl { get; set; }

    public string? LinkLabel { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedByUserId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedByUserId { get; set; }
}
