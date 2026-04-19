using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;

namespace NeighborGoods.Api.Shared.Notifications;

public sealed class AcsEmailSender(
    IOptions<EmailSenderOptions> options,
    ILogger<AcsEmailSender> logger) : IEmailSender
{
    private readonly EmailSenderOptions _options = options.Value;

    public async Task SendAsync(
        string toEmail,
        string subject,
        string plainTextContent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString) ||
            string.IsNullOrWhiteSpace(_options.FromEmailAddress))
        {
            throw new InvalidOperationException("EMAIL_NOT_CONFIGURED");
        }

        try
        {
            var emailClient = new EmailClient(_options.ConnectionString);
            var emailContent = new EmailContent(subject)
            {
                PlainText = plainTextContent
            };
            var recipients = new EmailRecipients([new EmailAddress(toEmail)]);
            var message = new EmailMessage(_options.FromEmailAddress, recipients, emailContent);
            await emailClient.SendAsync(WaitUntil.Started, message, cancellationToken);
        }
        catch (RequestFailedException ex)
        {
            logger.LogWarning(
                ex,
                "發送 Email 失敗：Status={Status}, ErrorCode={ErrorCode}, Email={Email}",
                ex.Status,
                ex.ErrorCode,
                toEmail);
            throw;
        }
    }
}
