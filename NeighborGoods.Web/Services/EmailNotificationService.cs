using System.Net;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeighborGoods.Web.Models.Configuration;
using NeighborGoods.Web.Models.Enums;

namespace NeighborGoods.Web.Services;

/// <summary>
/// Email 通知服務實作
/// 完全模仿 LINE 通知的錯誤處理邏輯和行為
/// </summary>
public class EmailNotificationService : IEmailNotificationService
{
    private readonly EmailClient _emailClient;
    private readonly EmailNotificationOptions _options;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IOptions<EmailNotificationOptions> options,
        ILogger<EmailNotificationService> logger)
    {
        _options = options.Value;
        _logger = logger;

        // 建立 Email 客戶端
        _emailClient = new EmailClient(_options.ConnectionString);
    }

    public async Task SendPushMessageAsync(string email, string message, NotificationPriority priority)
    {
        try
        {
            var emailContent = new EmailContent("NeighborGoods 通知")
            {
                PlainText = message
            };

            var emailRecipients = new EmailRecipients(new[] { new EmailAddress(email) });

            var emailMessage = new EmailMessage(
                _options.FromEmailAddress,
                emailRecipients,
                emailContent);

            var emailSendOperation = await _emailClient.SendAsync(
                WaitUntil.Started,
                emailMessage);

            _logger.LogDebug("Email 推播訊息已發送：Email={Email}, Priority={Priority}, OperationId={OperationId}",
                email, priority, emailSendOperation.Id);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(
                "Email 推播訊息失敗：Status={Status}, ErrorCode={ErrorCode}, Response={Response}, Email={Email}, Priority={Priority}",
                ex.Status, ex.ErrorCode, ex.Message, email, priority);

            // 處理特定錯誤（模仿 LINE 通知的錯誤處理）
            if (ex.Status == 401)
            {
                _logger.LogError("Azure Communication Services 連線字串無效或已過期");
            }
            else if (ex.Status == 429)
            {
                _logger.LogWarning("Azure Communication Services Email 額度已用完（429 Too Many Requests）");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送 Email 推播訊息時發生錯誤：Email={Email}, Priority={Priority}", email, priority);
            // 不拋出異常，避免影響主要功能（與 LINE 通知行為一致）
        }
    }

    public async Task SendPushMessageWithLinkAsync(string email, string message, string linkUrl, string linkText, NotificationPriority priority)
    {
        try
        {
            // 建立 HTML Email 內容（對應 LINE 的按鈕模板）
            var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f5f5f5;
        }}
        .container {{
            background-color: #ffffff;
            border-radius: 8px;
            padding: 30px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .message {{
            margin-bottom: 30px;
            font-size: 16px;
        }}
        .button {{
            display: inline-block;
            padding: 12px 24px;
            background-color: #007bff;
            color: #ffffff;
            text-decoration: none;
            border-radius: 4px;
            font-weight: 500;
            text-align: center;
        }}
        .button:hover {{
            background-color: #0056b3;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""message"">
            {WebUtility.HtmlEncode(message)}
        </div>
        <a href=""{WebUtility.HtmlEncode(linkUrl)}"" class=""button"">
            {WebUtility.HtmlEncode(linkText)}
        </a>
    </div>
</body>
</html>";

            var plainTextContent = $"{message}\n\n{linkText}: {linkUrl}";

            var emailContent = new EmailContent("NeighborGoods 通知")
            {
                PlainText = plainTextContent,
                Html = htmlContent
            };

            var emailRecipients = new EmailRecipients(new[] { new EmailAddress(email) });

            var emailMessage = new EmailMessage(
                _options.FromEmailAddress,
                emailRecipients,
                emailContent);

            var emailSendOperation = await _emailClient.SendAsync(
                WaitUntil.Started,
                emailMessage);

            _logger.LogDebug("Email 推播訊息（帶連結）已發送：Email={Email}, Priority={Priority}, OperationId={OperationId}",
                email, priority, emailSendOperation.Id);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(
                "Email 推播訊息（帶連結）失敗：Status={Status}, ErrorCode={ErrorCode}, Response={Response}, Email={Email}, Priority={Priority}",
                ex.Status, ex.ErrorCode, ex.Message, email, priority);

            // 處理特定錯誤（模仿 LINE 通知的錯誤處理）
            if (ex.Status == 401)
            {
                _logger.LogError("Azure Communication Services 連線字串無效或已過期");
            }
            else if (ex.Status == 429)
            {
                _logger.LogWarning("Azure Communication Services Email 額度已用完（429 Too Many Requests）");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送 Email 推播訊息（帶連結）時發生錯誤：Email={Email}, Priority={Priority}", email, priority);
            // 不拋出異常，避免影響主要功能（與 LINE 通知行為一致）
        }
    }
}
