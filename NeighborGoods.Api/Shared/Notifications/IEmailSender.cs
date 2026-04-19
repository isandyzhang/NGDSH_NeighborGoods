namespace NeighborGoods.Api.Shared.Notifications;

public interface IEmailSender
{
    Task SendAsync(
        string toEmail,
        string subject,
        string plainTextContent,
        CancellationToken cancellationToken = default);
}
