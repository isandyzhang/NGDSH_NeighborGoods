using Microsoft.Extensions.Options;
using NeighborGoods.Api.Shared.Notifications;

namespace NeighborGoods.Api.Features.Integrations.Line.Services;

public sealed class LineFlexMessageBuilder(IOptions<LineMessagingOptions> options)
{
    private readonly LineMessagingOptions _options = options.Value;

    public LineFlexMessage BuildHomeCard()
    {
        return BuildCard(
            title: "NeighborGoods 首頁",
            message: "快速回到首頁，查看最新社區商品。",
            buttonLabel: "前往首頁",
            buttonUrl: BuildUrl("/"));
    }

    public LineFlexMessage BuildMyListingsCard(LineMyListingsSummary summary)
    {
        return BuildCard(
            title: "我的商品",
            message: $"總數 {summary.Total}｜刊登中 {summary.Active}｜保留中 {summary.Reserved}｜已售出 {summary.Sold}",
            buttonLabel: "前往我的商品",
            buttonUrl: BuildUrl("/my-listings"));
    }

    public LineFlexMessage BuildMyMessagesCard(LineMyMessagesSummary summary)
    {
        return BuildCard(
            title: "我的訊息",
            message: $"目前有 {summary.ConversationCount} 個對話，未讀 {summary.UnreadCount} 則。",
            buttonLabel: "前往我的訊息",
            buttonUrl: BuildUrl("/messages"));
    }

    public LineFlexMessage BuildNoticeCard(string title, string message, string? buttonLabel = null, string? buttonUrl = null)
    {
        return BuildCard(title, message, buttonLabel, buttonUrl);
    }

    public LineFlexMessage BuildBindHintCard()
    {
        return BuildCard(
            title: "尚未完成綁定",
            message: "請先到網站個人設定完成 LINE 通知綁定，才能查看個人摘要。",
            buttonLabel: "前往個人設定",
            buttonUrl: BuildUrl("/profile"));
    }

    private LineFlexMessage BuildCard(string title, string message, string? buttonLabel, string? buttonUrl)
    {
        if (!string.IsNullOrWhiteSpace(buttonLabel) && !string.IsNullOrWhiteSpace(buttonUrl))
        {
            return new LineFlexMessage(
                AltText: $"{title} - {message}",
                Contents: new
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
                                text = title,
                                weight = "bold",
                                size = "lg",
                                wrap = true
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
                                    label = buttonLabel,
                                    uri = buttonUrl
                                }
                            }
                        }
                    }
                });
        }

        return new LineFlexMessage(
            AltText: $"{title} - {message}",
            Contents: new
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
                            text = title,
                            weight = "bold",
                            size = "lg",
                            wrap = true
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
            });
    }

    private string BuildUrl(string path)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.WebBaseUrl) ? "http://localhost:5173" : _options.WebBaseUrl.TrimEnd('/');
        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            path = "/" + path;
        }

        return $"{baseUrl}{path}";
    }
}

public sealed record LineFlexMessage(string AltText, object Contents);
