using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NeighborGoods.Api.Features.Account.Services;
using NeighborGoods.Api.Shared.Notifications;

namespace NeighborGoods.Api.Features.Integrations.Line.Services;

public sealed class LineWebhookService(
    AccountLineBindingService lineBindingService,
    LineMenuQueryService lineMenuQueryService,
    LineFlexMessageBuilder flexMessageBuilder,
    ILineMessageSender lineMessageSender,
    IOptions<LineMessagingOptions> lineMessagingOptions)
{
    private readonly LineMessagingOptions _options = lineMessagingOptions.Value;

    public async Task<(bool Ok, string? ErrorCode, string? ErrorMessage)> ProcessAsync(
        string body,
        string? signature,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateSignature(body, signature))
        {
            return (false, "LINE_WEBHOOK_SIGNATURE_INVALID", "簽章驗證失敗");
        }

        foreach (var evt in ParseEvents(body))
        {
            if (string.IsNullOrWhiteSpace(evt.UserId))
            {
                continue;
            }

            if (string.Equals(evt.Type, "follow", StringComparison.OrdinalIgnoreCase))
            {
                await lineBindingService.HandleFollowAsync(evt.UserId, cancellationToken);
                continue;
            }

            if (string.Equals(evt.Type, "unfollow", StringComparison.OrdinalIgnoreCase))
            {
                await lineBindingService.HandleUnfollowAsync(evt.UserId, cancellationToken);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(evt.ReplyToken))
            {
                await HandleInteractiveEventAsync(evt, cancellationToken);
            }
        }

        return (true, null, null);
    }

    private async Task HandleInteractiveEventAsync(LineWebhookEventItem evt, CancellationToken cancellationToken)
    {
        var action = ResolveAction(evt);
        if (string.IsNullOrWhiteSpace(action))
        {
            var helpCard = flexMessageBuilder.BuildNoticeCard(
                "NeighborGoods 小幫手",
                "可輸入：首頁、我的商品、我的訊息，或使用下方圖文選單。");
            await lineMessageSender.ReplyFlexAsync(evt.ReplyToken!, helpCard.AltText, helpCard.Contents, cancellationToken);
            return;
        }

        if (action == "home")
        {
            var homeCard = flexMessageBuilder.BuildHomeCard();
            await lineMessageSender.ReplyFlexAsync(evt.ReplyToken!, homeCard.AltText, homeCard.Contents, cancellationToken);
            return;
        }

        var user = await lineMenuQueryService.GetBoundUserAsync(evt.UserId!, cancellationToken);
        if (user is null)
        {
            var bindHint = flexMessageBuilder.BuildBindHintCard();
            await lineMessageSender.ReplyFlexAsync(evt.ReplyToken!, bindHint.AltText, bindHint.Contents, cancellationToken);
            return;
        }

        if (action == "myListings")
        {
            var summary = await lineMenuQueryService.GetMyListingsSummaryAsync(user.Id, cancellationToken);
            var card = flexMessageBuilder.BuildMyListingsCard(summary);
            await lineMessageSender.ReplyFlexAsync(evt.ReplyToken!, card.AltText, card.Contents, cancellationToken);
            return;
        }

        if (action == "myMessages")
        {
            var summary = await lineMenuQueryService.GetMyMessagesSummaryAsync(user.Id, cancellationToken);
            var card = flexMessageBuilder.BuildMyMessagesCard(summary);
            await lineMessageSender.ReplyFlexAsync(evt.ReplyToken!, card.AltText, card.Contents, cancellationToken);
            return;
        }

        var fallback = flexMessageBuilder.BuildNoticeCard(
            "功能尚未支援",
            "目前可用功能：首頁、我的商品、我的訊息。");
        await lineMessageSender.ReplyFlexAsync(evt.ReplyToken!, fallback.AltText, fallback.Contents, cancellationToken);
    }

    private static string? ResolveAction(LineWebhookEventItem evt)
    {
        if (!string.IsNullOrWhiteSpace(evt.PostbackData))
        {
            var postbackAction = ParseActionFromPostback(evt.PostbackData!);
            if (!string.IsNullOrWhiteSpace(postbackAction))
            {
                return postbackAction;
            }
        }

        if (!string.Equals(evt.Type, "message", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var text = evt.MessageText?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text switch
        {
            "首頁" => "home",
            "我的商品" => "myListings",
            "我的訊息" => "myMessages",
            "menu:home" => "home",
            "menu:myListings" => "myListings",
            "menu:myMessages" => "myMessages",
            _ => null
        };
    }

    private static string? ParseActionFromPostback(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        if (data.StartsWith("menu:", StringComparison.OrdinalIgnoreCase))
        {
            return data["menu:".Length..];
        }

        var segments = data.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var segment in segments)
        {
            var idx = segment.IndexOf('=');
            if (idx <= 0 || idx >= segment.Length - 1)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(segment[..idx]);
            if (!string.Equals(key, "action", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = Uri.UnescapeDataString(segment[(idx + 1)..]);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }

    private bool ValidateSignature(string body, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(_options.ChannelSecret))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.ChannelSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var computedSignature = Convert.ToBase64String(hash);
        return string.Equals(computedSignature, signature, StringComparison.Ordinal);
    }

    private static IReadOnlyList<LineWebhookEventItem> ParseEvents(string body)
    {
        var result = new List<LineWebhookEventItem>();
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("events", out var eventsElement))
        {
            return result;
        }

        foreach (var eventElement in eventsElement.EnumerateArray())
        {
            var type = eventElement.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString() ?? string.Empty
                : string.Empty;

            string? userId = null;
            string? replyToken = eventElement.TryGetProperty("replyToken", out var replyTokenElement)
                ? replyTokenElement.GetString()
                : null;
            string? postbackData = null;
            string? messageType = null;
            string? messageText = null;

            if (eventElement.TryGetProperty("source", out var sourceElement) &&
                sourceElement.TryGetProperty("userId", out var userIdElement))
            {
                userId = userIdElement.GetString();
            }

            if (eventElement.TryGetProperty("postback", out var postbackElement)
                && postbackElement.TryGetProperty("data", out var dataElement))
            {
                postbackData = dataElement.GetString();
            }

            if (eventElement.TryGetProperty("message", out var messageElement))
            {
                messageType = messageElement.TryGetProperty("type", out var mtElement)
                    ? mtElement.GetString()
                    : null;
                messageText = messageElement.TryGetProperty("text", out var textElement)
                    ? textElement.GetString()
                    : null;
            }

            result.Add(new LineWebhookEventItem(type, userId, replyToken, postbackData, messageType, messageText));
        }

        return result;
    }

    private sealed record LineWebhookEventItem(
        string Type,
        string? UserId,
        string? ReplyToken,
        string? PostbackData,
        string? MessageType,
        string? MessageText);
}
