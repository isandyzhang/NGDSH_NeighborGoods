using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NeighborGoods.Api.Features.Account.Services;
using NeighborGoods.Api.Shared.Notifications;

namespace NeighborGoods.Api.Features.Integrations.Line.Services;

public sealed class LineWebhookService(
    AccountLineBindingService lineBindingService,
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
            }
            else if (string.Equals(evt.Type, "unfollow", StringComparison.OrdinalIgnoreCase))
            {
                await lineBindingService.HandleUnfollowAsync(evt.UserId, cancellationToken);
            }
        }

        return (true, null, null);
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
            if (eventElement.TryGetProperty("source", out var sourceElement) &&
                sourceElement.TryGetProperty("userId", out var userIdElement))
            {
                userId = userIdElement.GetString();
            }

            result.Add(new LineWebhookEventItem(type, userId));
        }

        return result;
    }

    private sealed record LineWebhookEventItem(string Type, string? UserId);
}
