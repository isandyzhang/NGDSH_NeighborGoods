using NeighborGoods.Api.Shared.Notifications;

namespace NeighborGoods.Api.Tests;

internal sealed class FakeLineMessageSender : ILineMessageSender
{
    public static List<(string UserId, string Message)> SentTextMessages { get; } = [];

    public static List<(string UserId, string Message, string LinkUrl, string LinkText)> SentLinkMessages { get; } = [];

    public Task SendTextAsync(string lineUserId, string message, CancellationToken cancellationToken = default)
    {
        SentTextMessages.Add((lineUserId, message));
        return Task.CompletedTask;
    }

    public Task SendLinkAsync(
        string lineUserId,
        string message,
        string linkUrl,
        string linkText,
        CancellationToken cancellationToken = default)
    {
        SentLinkMessages.Add((lineUserId, message, linkUrl, linkText));
        return Task.CompletedTask;
    }

    public static void Reset()
    {
        SentTextMessages.Clear();
        SentLinkMessages.Clear();
    }
}
