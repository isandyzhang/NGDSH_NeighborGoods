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

    public LineFlexMessage BuildMyListingsCarousel(
        LineMyListingsSummary summary,
        IReadOnlyList<LineMyListingCardItem> items)
    {
        var bubbles = new List<object>
        {
            BuildSummaryBubble(
                title: "我的商品總覽",
                message: $"總數 {summary.Total}｜刊登中 {summary.Active}｜保留中 {summary.Reserved}｜已售出 {summary.Sold}",
                buttonLabel: "前往我的商品",
                buttonUrl: BuildUrl("/my-listings"))
        };

        foreach (var item in items)
        {
            var priceText = item.IsFree ? "免費" : $"{item.Price:0} 元";
            var statusText = ToListingStatusText(item.Status);
            var unreadText = item.HasUnreadMessages ? "有未讀訊息" : "無未讀訊息";

            bubbles.Add(new
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
                            text = item.Title,
                            weight = "bold",
                            size = "lg",
                            wrap = true,
                            maxLines = 2
                        },
                        new
                        {
                            type = "text",
                            text = $"狀態：{statusText}",
                            size = "sm",
                            color = "#666666",
                            wrap = true
                        },
                        new
                        {
                            type = "text",
                            text = $"被收藏：{item.FavoriteCount}",
                            size = "sm",
                            color = "#666666"
                        },
                        new
                        {
                            type = "text",
                            text = $"價格：{priceText}",
                            size = "sm",
                            color = "#666666"
                        },
                        new
                        {
                            type = "text",
                            text = unreadText,
                            size = "sm",
                            color = item.HasUnreadMessages ? "#C2185B" : "#666666"
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
                                label = "查看商品",
                                uri = BuildUrl($"/listings/{item.ListingId}")
                            }
                        }
                    }
                }
            });
        }

        return new LineFlexMessage(
            AltText: $"我的商品：共 {summary.Total} 筆，顯示最多 5 筆重點商品",
            Contents: new
            {
                type = "carousel",
                contents = bubbles.ToArray()
            });
    }

    public LineFlexMessage BuildMyMessagesCard(LineMyMessagesSummary summary)
    {
        return BuildCard(
            title: "我的訊息",
            message: $"目前有 {summary.ConversationCount} 個對話，未讀 {summary.UnreadCount} 則。",
            buttonLabel: "前往我的訊息",
            buttonUrl: BuildUrl("/messages"));
    }

    public LineFlexMessage BuildMyMessagesQuickLinksCard(LineMyMessagesSummary summary)
    {
        var quickLinkRows = summary.UnreadQuickLinks
            .Select(x => new
            {
                type = "button",
                style = "link",
                action = new
                {
                    type = "uri",
                    label = $"{TrimToLabel(x.OtherDisplayName)}（未讀{x.UnreadCount}）",
                    uri = BuildUrl($"/messages/{x.ConversationId}")
                }
            })
            .Cast<object>()
            .ToList();

        var bodyContents = new List<object>
        {
            new
            {
                type = "text",
                text = "我的訊息",
                weight = "bold",
                size = "lg",
                wrap = true
            },
            new
            {
                type = "text",
                text = $"目前有 {summary.ConversationCount} 個對話，未讀 {summary.UnreadCount} 則。",
                wrap = true,
                size = "md"
            }
        };

        if (quickLinkRows.Count > 0)
        {
            bodyContents.Add(new
            {
                type = "separator",
                margin = "md"
            });

            bodyContents.Add(new
            {
                type = "text",
                text = "最近未讀對話",
                size = "sm",
                color = "#666666",
                margin = "md"
            });
        }

        return new LineFlexMessage(
            AltText: $"我的訊息：未讀 {summary.UnreadCount} 則",
            Contents: new
            {
                type = "bubble",
                body = new
                {
                    type = "box",
                    layout = "vertical",
                    spacing = "md",
                    contents = bodyContents.ToArray()
                },
                footer = new
                {
                    type = "box",
                    layout = "vertical",
                    spacing = "sm",
                    contents = quickLinkRows
                        .Append(new
                        {
                            type = "button",
                            style = "primary",
                            action = new
                            {
                                type = "uri",
                                label = "查看全部訊息",
                                uri = BuildUrl("/messages")
                            }
                        })
                        .ToArray()
                }
            });
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

    private static object BuildSummaryBubble(string title, string message, string buttonLabel, string buttonUrl)
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
        };
    }

    private static string ToListingStatusText(int status)
    {
        return status switch
        {
            0 => "刊登中",
            1 => "保留中",
            2 => "已售出",
            _ => "未知"
        };
    }

    private static string TrimToLabel(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "對話";
        }

        var trimmed = text.Trim();
        return trimmed.Length <= 12 ? trimmed : trimmed[..12] + "...";
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
