namespace NeighborGoods.Api.Shared.Notifications;

public sealed class EmailSenderOptions
{
    public const string SectionName = "EmailNotification";

    public string ConnectionString { get; set; } = string.Empty;

    public string FromEmailAddress { get; set; } = string.Empty;
}
