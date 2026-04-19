namespace NeighborGoods.Api.Shared.Notifications;

public interface ILineMessageSender
{
    Task SendTextAsync(
        string lineUserId,
        string message,
        CancellationToken cancellationToken = default);

    Task SendLinkAsync(
        string lineUserId,
        string message,
        string linkUrl,
        string linkText,
        CancellationToken cancellationToken = default);
}
