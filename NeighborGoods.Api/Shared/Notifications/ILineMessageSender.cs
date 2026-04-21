namespace NeighborGoods.Api.Shared.Notifications;

public interface ILineMessageSender
{
    Task ReplyFlexAsync(
        string replyToken,
        string altText,
        object flexContents,
        CancellationToken cancellationToken = default);

    Task PushFlexAsync(
        string lineUserId,
        string altText,
        object flexContents,
        CancellationToken cancellationToken = default);

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
