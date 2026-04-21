using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NeighborGoods.Api.Shared.Notifications;

public sealed class LineMessageSender(
    HttpClient httpClient,
    IOptions<LineMessagingOptions> options,
    LinePushQuotaTracker pushQuotaTracker,
    ILogger<LineMessageSender> logger) : ILineMessageSender
{
    private readonly LineMessagingOptions _options = options.Value;

    public async Task ReplyFlexAsync(
        string replyToken,
        string altText,
        object flexContents,
        CancellationToken cancellationToken = default)
    {
        if (!HasAccessTokenConfigured())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(replyToken))
        {
            logger.LogWarning("LINE reply 略過：replyToken 為空");
            return;
        }

        var payload = new
        {
            replyToken,
            messages =
                new[]
                {
                    new
                    {
                        type = "flex",
                        altText,
                        contents = flexContents
                    }
                }
        };

        await PostPayloadAsync("/message/reply", payload, replyToken, isPush: false, cancellationToken);
    }

    public async Task PushFlexAsync(
        string lineUserId,
        string altText,
        object flexContents,
        CancellationToken cancellationToken = default)
    {
        if (!HasAccessTokenConfigured())
        {
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
                        type = "flex",
                        altText,
                        contents = flexContents
                    }
                }
        };

        await PostPayloadAsync("/message/push", payload, lineUserId, isPush: true, cancellationToken);
    }

    public async Task SendTextAsync(
        string lineUserId,
        string message,
        CancellationToken cancellationToken = default)
    {
        var flexContents = BuildSimpleFlex(message, null, null);
        await PushFlexAsync(lineUserId, message, flexContents, cancellationToken);
    }

    public async Task SendLinkAsync(
        string lineUserId,
        string message,
        string linkUrl,
        string linkText,
        CancellationToken cancellationToken = default)
    {
        var flexContents = BuildSimpleFlex(message, linkText, linkUrl);
        await PushFlexAsync(lineUserId, message, flexContents, cancellationToken);
    }

    private bool HasAccessTokenConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ChannelAccessToken))
        {
            logger.LogWarning("LINE 訊息略過：未設定 ChannelAccessToken");
            return false;
        }

        return true;
    }

    private async Task PostPayloadAsync(
        string endpoint,
        object payload,
        string targetId,
        bool isPush,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}{endpoint}");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ChannelAccessToken);
            var json = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning(
                    "LINE 訊息失敗：Status={StatusCode}, Target={TargetId}, Endpoint={Endpoint}, Response={Response}",
                    response.StatusCode,
                    targetId,
                    endpoint,
                    responseContent);
            }
            else if (isPush)
            {
                pushQuotaTracker.Increment();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LINE 訊息發生例外：Target={TargetId}, Endpoint={Endpoint}", targetId, endpoint);
        }
    }

    private static object BuildSimpleFlex(string message, string? buttonText, string? buttonUrl)
    {
        if (!string.IsNullOrWhiteSpace(buttonText) && !string.IsNullOrWhiteSpace(buttonUrl))
        {
            return new
            {
                type = "bubble",
                body = new
                {
                    type = "box",
                    layout = "vertical",
                    spacing = "md",
                    contents = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = "NeighborGoods",
                            weight = "bold",
                            size = "lg"
                        },
                        new
                        {
                            type = "text",
                            text = message,
                            wrap = true,
                            size = "md"
                        }
                    }
                },
                footer = new
                {
                    type = "box",
                    layout = "vertical",
                    contents = new object[]
                    {
                        new
                        {
                            type = "button",
                            style = "primary",
                            action = new
                            {
                                type = "uri",
                                label = buttonText,
                                uri = buttonUrl
                            }
                        }
                    }
                }
            };
        }

        return new
        {
            type = "bubble",
            body = new
            {
                type = "box",
                layout = "vertical",
                spacing = "md",
                contents = new object[]
                {
                    new
                    {
                        type = "text",
                        text = "NeighborGoods",
                        weight = "bold",
                        size = "lg"
                    },
                    new
                    {
                        type = "text",
                        text = message,
                        wrap = true,
                        size = "md"
                    }
                }
            }
        };
    }
}
