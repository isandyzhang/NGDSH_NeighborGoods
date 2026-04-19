using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NeighborGoods.Api.Shared.Notifications;

public sealed class LineMessageSender(
    HttpClient httpClient,
    IOptions<LineMessagingOptions> options,
    ILogger<LineMessageSender> logger) : ILineMessageSender
{
    private readonly LineMessagingOptions _options = options.Value;

    public async Task SendTextAsync(
        string lineUserId,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ChannelAccessToken))
        {
            logger.LogWarning("LINE 推播略過：未設定 ChannelAccessToken");
            return;
        }

        var payload = new
        {
            to = lineUserId,
            messages =
                new[]
                {
                    new
                    {
                        type = "text",
                        text = message
                    }
                }
        };
        await PostPayloadAsync(payload, lineUserId, cancellationToken);
    }

    public async Task SendLinkAsync(
        string lineUserId,
        string message,
        string linkUrl,
        string linkText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ChannelAccessToken))
        {
            logger.LogWarning("LINE 推播略過：未設定 ChannelAccessToken");
            return;
        }

        var payload = new
        {
            to = lineUserId,
            messages =
                new[]
                {
                    new
                    {
                        type = "template",
                        altText = message,
                        template = new
                        {
                            type = "buttons",
                            text = message,
                            actions =
                                new[]
                                {
                                    new
                                    {
                                        type = "uri",
                                        label = linkText,
                                        uri = linkUrl
                                    }
                                }
                        }
                    }
                }
        };
        await PostPayloadAsync(payload, lineUserId, cancellationToken);
    }

    private async Task PostPayloadAsync(object payload, string lineUserId, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/message/push");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ChannelAccessToken);
            var json = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning(
                    "LINE 推播失敗：Status={StatusCode}, UserId={LineUserId}, Response={Response}",
                    response.StatusCode,
                    lineUserId,
                    responseContent);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LINE 推播發生例外：UserId={LineUserId}", lineUserId);
        }
    }
}
