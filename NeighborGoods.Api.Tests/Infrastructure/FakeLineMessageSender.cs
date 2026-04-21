using NeighborGoods.Api.Shared.Notifications;

namespace NeighborGoods.Api.Tests;

internal sealed class FakeLineMessageSender : ILineMessageSender
{
    public static List<(string ReplyToken, string AltText)> ReplyFlexMessages { get; } = [];
    public static List<(string UserId, string AltText)> PushFlexMessages { get; } = [];

    public Task ReplyFlexAsync(
        string replyToken,
        string altText,
        object flexContents,
        CancellationToken cancellationToken = default)
    {
        ReplyFlexMessages.Add((replyToken, altText));
        return Task.CompletedTask;
    }

    public Task PushFlexAsync(
        string lineUserId,
        string altText,
        object flexContents,
        CancellationToken cancellationToken = default)
    {
        PushFlexMessages.Add((lineUserId, altText));
        return Task.CompletedTask;
    }

    public Task SendTextAsync(string lineUserId, string message, CancellationToken cancellationToken = default)
    {
        return PushFlexAsync(lineUserId, message, new { legacy = true, message }, cancellationToken);
    }

    public Task SendLinkAsync(
        string lineUserId,
        string message,
        string linkUrl,
        string linkText,
        CancellationToken cancellationToken = default)
    {
        return PushFlexAsync(
            lineUserId,
            message,
            new { legacy = true, message, linkUrl, linkText },
            cancellationToken);
    }

    public static void Reset()
    {
        ReplyFlexMessages.Clear();
        PushFlexMessages.Clear();
    }
}
