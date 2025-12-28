using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeighborGoods.Web.Models.Configuration;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Services;

namespace NeighborGoods.Web.Services;

/// <summary>
/// LINE Messaging API 服務實作
/// </summary>
public class LineMessagingApiService : ILineMessagingApiService
{
    private readonly HttpClient _httpClient;
    private readonly LineMessagingApiOptions _options;
    private readonly ILogger<LineMessagingApiService> _logger;
    private const string ApiBaseUrl = "https://api.line.me/v2/bot";

    public LineMessagingApiService(
        HttpClient httpClient,
        IOptions<LineMessagingApiOptions> options,
        ILogger<LineMessagingApiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        // 設定 HttpClient 的預設 Header
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.ChannelAccessToken}");
    }

    public async Task SendPushMessageAsync(string userId, string message, NotificationPriority priority)
    {
        try
        {
            var payload = new
            {
                to = userId,
                messages = new[]
                {
                    new
                    {
                        type = "text",
                        text = message
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{ApiBaseUrl}/message/push", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "LINE 推播訊息失敗：Status={Status}, Response={Response}, UserId={UserId}, Priority={Priority}",
                    response.StatusCode, responseContent, userId, priority);

                // 處理特定錯誤
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("LINE Channel Access Token 無效或已過期");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("LINE Messaging API 額度已用完（429 Too Many Requests）");
                }
            }
            else
            {
                _logger.LogDebug("LINE 推播訊息成功：UserId={UserId}, Priority={Priority}", userId, priority);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送 LINE 推播訊息時發生錯誤：UserId={UserId}, Priority={Priority}", userId, priority);
            // 不拋出異常，避免影響主要功能
        }
    }

    public async Task SendPushMessageWithLinkAsync(string userId, string message, string linkUrl, string linkText, NotificationPriority priority)
    {
        try
        {
            var payload = new
            {
                to = userId,
                messages = new[]
                {
                    new
                    {
                        type = "template",
                        altText = message,
                        template = new
                        {
                            type = "buttons",
                            text = message,
                            actions = new[]
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

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{ApiBaseUrl}/message/push", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "LINE 推播訊息（帶連結）失敗：Status={Status}, Response={Response}, UserId={UserId}, Priority={Priority}",
                    response.StatusCode, responseContent, userId, priority);

                // 處理特定錯誤
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("LINE Channel Access Token 無效或已過期");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("LINE Messaging API 額度已用完（429 Too Many Requests）");
                }
            }
            else
            {
                _logger.LogDebug("LINE 推播訊息（帶連結）成功：UserId={UserId}, Priority={Priority}", userId, priority);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送 LINE 推播訊息（帶連結）時發生錯誤：UserId={UserId}, Priority={Priority}", userId, priority);
            // 不拋出異常，避免影響主要功能
        }
    }

    public bool ValidateWebhookSignature(string body, string signature)
    {
        try
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.ChannelSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
            var computedSignature = Convert.ToBase64String(hash);

            return computedSignature == signature;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驗證 Webhook 簽章時發生錯誤");
            return false;
        }
    }

    public List<LineWebhookEvent> ParseWebhookEvents(string body)
    {
        var events = new List<LineWebhookEvent>();

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("events", out var eventsElement))
            {
                foreach (var eventElement in eventsElement.EnumerateArray())
                {
                    var webhookEvent = new LineWebhookEvent
                    {
                        Type = eventElement.TryGetProperty("type", out var typeElement)
                            ? typeElement.GetString() ?? string.Empty
                            : string.Empty
                    };

                    // 取得 source.userId（如果存在）
                    if (eventElement.TryGetProperty("source", out var sourceElement))
                    {
                        if (sourceElement.TryGetProperty("userId", out var userIdElement))
                        {
                            webhookEvent.UserId = userIdElement.GetString();
                        }
                    }

                    // 取得 timestamp
                    if (eventElement.TryGetProperty("timestamp", out var timestampElement))
                    {
                        var timestamp = timestampElement.GetInt64();
                        webhookEvent.Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
                    }

                    // 儲存其他資料
                    webhookEvent.Data = new Dictionary<string, object>();
                    foreach (var prop in eventElement.EnumerateObject())
                    {
                        if (prop.Name != "type" && prop.Name != "source" && prop.Name != "timestamp")
                        {
                            webhookEvent.Data[prop.Name] = prop.Value.GetRawText();
                        }
                    }

                    events.Add(webhookEvent);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析 Webhook 事件時發生錯誤");
        }

        return events;
    }
}

