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
        var bubbles = new List<object>();

        foreach (var item in items)
        {
            var priceText = item.IsFree ? "免費" : $"{item.Price:0} 元";
            var (statusText, statusColor) = GetListingStatusBadge(item.Status);
            var overlayColor = item.Status == 0 ? "#03303Acc" : "#9C8E7Ecc";
            var imageUrl = string.IsNullOrWhiteSpace(item.ImageUrl)
                ? "https://developers-resource.landpress.line.me/fx/img/01_1_cafe.png"
                : item.ImageUrl;
            var favoriteText = $"❤ {item.FavoriteCount}";

            bubbles.Add(new
            {
                type = "bubble",
                body = new
                {
                    type = "box",
                    layout = "vertical",
                    contents = new object[]
                    {
                        new
                        {
                            type = "box",
                            layout = "vertical",
                            contents = new object[]
                            {
                                new
                                {
                                    type = "image",
                                    url = imageUrl,
                                    size = "full",
                                    aspectMode = "cover",
                                    aspectRatio = "2:1",
                                    gravity = "top"
                                },
                                new
                                {
                                    type = "box",
                                    layout = "vertical",
                                    position = "absolute",
                                    cornerRadius = "20px",
                                    offsetTop = "12px",
                                    offsetStart = "12px",
                                    height = "25px",
                                    paddingStart = "8px",
                                    paddingEnd = "8px",
                                    backgroundColor = statusColor,
                                    contents = new object[]
                                    {
                                        new
                                        {
                                            type = "text",
                                            text = statusText,
                                            color = "#ffffff",
                                            align = "center",
                                            size = "xs",
                                            offsetTop = "3px"
                                        }
                                    }
                                }
                            }
                        },
                        new
                        {
                            type = "box",
                            layout = "vertical",
                            backgroundColor = overlayColor,
                            paddingAll = "16px",
                            contents = new object[]
                            {
                                new
                                {
                                    type = "text",
                                    text = item.Title,
                                    color = "#ffffff",
                                    size = "xl",
                                    weight = "bold",
                                    wrap = true,
                                    maxLines = 2
                                },
                                new
                                {
                                    type = "text",
                                    text = priceText,
                                    color = "#ebebeb",
                                    size = "sm",
                                    margin = "md"
                                },
                                new
                                {
                                    type = "box",
                                    layout = "horizontal",
                                    margin = "sm",
                                    contents = new object[]
                                    {
                                        new
                                        {
                                            type = "text",
                                            text = favoriteText,
                                            color = "#ffffff",
                                            size = "lg",
                                            weight = "bold",
                                            flex = 1
                                        },
                                        BuildUnreadBadge(item.UnreadCount, BuildUrl("/messages"))
                                    }
                                },
                                new
                                {
                                    type = "box",
                                    layout = "vertical",
                                    cornerRadius = "8px",
                                    margin = "xxl",
                                    height = "40px",
                                    contents = new object[]
                                    {
                                        new
                                        {
                                            type = "button",
                                            style = "primary",
                                            color = "#1DB446",
                                            height = "sm",
                                            action = new
                                            {
                                                type = "uri",
                                                label = "查看商品詳細",
                                                uri = BuildUrl($"/listings/{item.ListingId}")
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    paddingAll = "0px"
                }
            });
        }

        return new LineFlexMessage(
            AltText: $"我的商品：共 {summary.Total} 筆，顯示最多 3 筆刊登中/保留中商品",
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
        var conversationRows = summary.RecentConversations
            .Select((x, index) =>
            {
                var rowItems = new List<object>
                {
                    new
                    {
                        type = "box",
                        layout = "horizontal",
                        contents = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = x.OtherDisplayName,
                                size = "xl",
                                weight = "bold",
                                color = "#111111",
                                flex = 1
                            },
                            BuildUnreadBadge(x.UnreadCount, BuildUrl("/messages"))
                        }
                    },
                    new
                    {
                        type = "text",
                        text = x.ListingTitle,
                        size = "sm",
                        color = "#555555",
                        wrap = true
                    },
                    new
                    {
                        type = "text",
                        text = x.LatestMessageContent,
                        size = "sm",
                        color = "#111111",
                        wrap = true,
                        maxLines = 2
                    },
                    new
                    {
                        type = "text",
                        text = x.LatestMessageAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                        size = "xs",
                        color = "#aaaaaa"
                    }
                };

                var blocks = new List<object>
                {
                    new
                    {
                        type = "box",
                        layout = "vertical",
                        margin = "md",
                        spacing = "sm",
                        contents = rowItems.ToArray()
                    }
                };

                if (index < summary.RecentConversations.Count - 1)
                {
                    blocks.Add(new
                    {
                        type = "separator",
                        margin = "lg"
                    });
                }

                return blocks;
            })
            .SelectMany(x => x)
            .ToList();

        var bodyContents = new List<object>
        {
            new
            {
                type = "text",
                text = "我的訊息",
                weight = "bold",
                color = "#1DB446",
                size = "sm",
                wrap = true
            },
            new
            {
                type = "text",
                text = summary.UserDisplayName,
                weight = "bold",
                size = "xxl",
                margin = "md",
                wrap = true,
            },
            new
            {
                type = "text",
                text = $"註冊日期：{(summary.RegisteredAt ?? DateTime.UtcNow).ToLocalTime():yyyy-MM-dd}",
                size = "xs",
                color = "#aaaaaa",
                wrap = true
            },
            new
            {
                type = "separator",
                margin = "xxl"
            }
        };

        if (conversationRows.Count > 0)
        {
            bodyContents.AddRange(conversationRows);
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
                    contents = new object[]
                    {
                        new
                        {
                            type = "button",
                            style = "primary",
                            height = "sm",
                            action = new
                            {
                                type = "uri",
                                label = "前往網站查看全部訊息",
                                uri = BuildUrl("/messages")
                            }
                        }
                    }
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

    private static (string Text, string Color) GetListingStatusBadge(int status)
    {
        return status switch
        {
            0 => ("刊登中", "#2E7D32"),
            1 => ("保留中", "#EF6C00"),
            2 => ("已售出", "#757575"),
            _ => ("未知", "#607D8B")
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

    private static object BuildUnreadBadge(int unreadCount, string messagesUrl)
    {
        if (unreadCount <= 0)
        {
            return new
            {
                type = "box",
                layout = "vertical",
                width = "76px",
                height = "34px",
                contents = new object[] { new { type = "filler" } }
            };
        }

        var badgeText = unreadCount > 99 ? "未讀 99+" : $"未讀 {unreadCount}";
        return new
        {
            type = "button",
            style = "primary",
            color = "#FF334B",
            height = "sm",
            action = new
            {
                type = "uri",
                label = badgeText,
                uri = messagesUrl
            }
        };
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
