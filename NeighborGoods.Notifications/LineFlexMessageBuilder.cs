using Microsoft.Extensions.Options;

namespace NeighborGoods.Notifications;

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
        if (items.Count == 0)
        {
            return new LineFlexMessage(
                AltText: "你目前沒有任何商品",
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
                                text = "你目前沒有任何商品",
                                weight = "bold",
                                size = "lg",
                                wrap = true
                            },
                            new
                            {
                                type = "text",
                                text = "先去刊登一個商品，或前往我的商品頁面查看。",
                                size = "sm",
                                color = "#666666",
                                wrap = true
                            }
                        }
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
                                color = "#1DB446",
                                action = new
                                {
                                    type = "uri",
                                    label = "前往刊登",
                                    uri = BuildUrl("/listings/create")
                                }
                            },
                            new
                            {
                                type = "button",
                                style = "link",
                                action = new
                                {
                                    type = "uri",
                                    label = "前往我的商品",
                                    uri = BuildUrl("/my-listings")
                                }
                            }
                        }
                    }
                });
        }

        var bubbles = new List<object>();

        foreach (var item in items)
        {
            var priceText = item.IsFree ? "免費" : $"{item.Price:0} 元";
            var (statusText, statusColor) = GetListingStatusBadge(item.Status);
            var imageUrl = string.IsNullOrWhiteSpace(item.ImageUrl)
                ? "https://developers-resource.landpress.line.me/fx/img/01_1_cafe.png"
                : item.ImageUrl;
            var metaText = $"{priceText}｜收藏 {item.FavoriteCount}｜未讀 {item.UnreadCount}";
            var listingMetaText = $"{item.ResidenceName}｜{item.PickupLocationName}";
            var descriptionText = string.IsNullOrWhiteSpace(item.Description) ? "無詳細說明" : item.Description.Trim();
            var createdAtText = $"刊登日期 {item.CreatedAt.ToLocalTime():yyyy-MM-dd}";
            var listingUrl = BuildUrl($"/listings/{item.ListingId}");

            bubbles.Add(new
            {
                type = "bubble",
                hero = new
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
                            aspectRatio = "20:13",
                            aspectMode = "cover",
                            action = new
                            {
                                type = "uri",
                                uri = listingUrl
                            }
                        },
                        new
                        {
                            type = "box",
                            layout = "vertical",
                            position = "absolute",
                            cornerRadius = "16px",
                            offsetTop = "12px",
                            offsetStart = "12px",
                            height = "32px",
                            paddingStart = "10px",
                            paddingEnd = "10px",
                            backgroundColor = statusColor,
                            justifyContent = "center",
                            contents = new object[]
                            {
                                new
                                {
                                    type = "text",
                                    text = statusText,
                                    color = "#ffffff",
                                    align = "center",
                                    size = "sm",
                                    weight = "bold"
                                }
                            }
                        }
                    }
                },
                body = new
                {
                    type = "box",
                    layout = "vertical",
                    spacing = "sm",
                    paddingAll = "16px",
                    contents = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = item.Title,
                            color = "#111111",
                            size = "xl",
                            weight = "bold",
                            wrap = true,
                            maxLines = 2
                        },
                        new
                        {
                            type = "text",
                            text = descriptionText,
                            color = "#555555",
                            size = "sm",
                            wrap = true,
                            maxLines = 2
                        },
                        new
                        {
                            type = "separator",
                            margin = "md"
                        },
                        new
                        {
                            type = "text",
                            text = metaText,
                            color = "#111111",
                            size = "sm",
                            wrap = true
                        },
                        new
                        {
                            type = "text",
                            text = listingMetaText,
                            color = "#111111",
                            size = "sm",
                            wrap = true
                        },
                        new
                        {
                            type = "separator",
                            margin = "md"
                        },
                        new
                        {
                            type = "text",
                            text = createdAtText,
                            color = "#888888",
                            size = "xs",
                            wrap = true
                        }
                    }
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
                            color = "#1DB446",
                            height = "sm",
                            action = new
                            {
                                type = "uri",
                                label = "查看商品詳細",
                                uri = listingUrl
                            }
                        }
                    },
                    flex = 0
                }
            });
        }

        return new LineFlexMessage(
            AltText: $"我的商品：共 {summary.Total} 筆，顯示最多 5 筆刊登中/保留中商品",
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
                var safeListingTitle = string.IsNullOrWhiteSpace(x.ListingTitle) ? "未命名商品" : x.ListingTitle.Trim();
                var safeDisplayName = string.IsNullOrWhiteSpace(x.OtherDisplayName) ? "對方" : x.OtherDisplayName.Trim();
                var rowItems = new List<object>
                {
                    new
                    {
                        type = "box",
                        layout = "horizontal",
                        alignItems = "center",
                        contents = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = safeListingTitle,
                                size = "lg",
                                weight = "bold",
                                color = "#111111",
                                flex = 1,
                                wrap = true,
                                maxLines = 2
                            },
                            BuildUnreadBadge(x.UnreadCount, BuildUrl("/messages"))
                        }
                    },
                    new
                    {
                        type = "text",
                        text = $"訊息者：{safeDisplayName}",
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

    public LineFlexMessage BuildListingExpiryNotice(IReadOnlyList<LineListingExpiryItem> items)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("At least one listing is required.", nameof(items));
        }

        if (items.Count == 1)
        {
            return BuildListingExpiryBubble(items[0]);
        }

        var bubbles = items.Take(10).Select(BuildListingExpiryBubble).Cast<object>().ToList();
        return new LineFlexMessage(
            AltText: $"刊登到期通知｜{items.Count} 件商品",
            Contents: new
            {
                type = "carousel",
                contents = bubbles
            });
    }

    private LineFlexMessage BuildListingExpiryBubble(LineListingExpiryItem item)
    {
        var safeTitle = string.IsNullOrWhiteSpace(item.Title) ? "未命名商品" : item.Title.Trim();
        var priceText = item.IsFree ? "免費" : $"NT$ {item.Price:0}";
        var detailUrl = BuildUrl($"/listings/{item.ListingId}");
        var imageUrl = string.IsNullOrWhiteSpace(item.ImageUrl)
            ? "https://developers-resource.landpress.line.me/fx/img/01_1_cafe.png"
            : item.ImageUrl;

        return new LineFlexMessage(
            AltText: $"刊登到期｜{safeTitle}",
            Contents: new
            {
                type = "bubble",
                hero = new
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
                            aspectRatio = "20:13",
                            aspectMode = "cover",
                            action = new { type = "uri", uri = detailUrl }
                        },
                        new
                        {
                            type = "box",
                            layout = "vertical",
                            position = "absolute",
                            cornerRadius = "16px",
                            offsetTop = "12px",
                            offsetStart = "12px",
                            height = "32px",
                            paddingStart = "10px",
                            paddingEnd = "10px",
                            backgroundColor = "#9CA3AF",
                            justifyContent = "center",
                            contents = new object[]
                            {
                                new
                                {
                                    type = "text",
                                    text = "刊登已到期",
                                    color = "#ffffff",
                                    align = "center",
                                    size = "sm",
                                    weight = "bold"
                                }
                            }
                        }
                    }
                },
                body = new
                {
                    type = "box",
                    layout = "vertical",
                    spacing = "sm",
                    paddingAll = "16px",
                    contents = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = safeTitle,
                            weight = "bold",
                            size = "lg",
                            wrap = true
                        },
                        new
                        {
                            type = "text",
                            text = "此商品已刊登滿 14 天，目前為非活躍狀態。",
                            size = "sm",
                            color = "#666666",
                            wrap = true
                        },
                        new
                        {
                            type = "text",
                            text = $"{priceText}｜{item.CategoryName}",
                            size = "sm",
                            color = "#111111",
                            wrap = true
                        }
                    }
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
                            color = "#1DB446",
                            action = new
                            {
                                type = "uri",
                                label = "延續刊登商品",
                                uri = detailUrl
                            }
                        },
                        new
                        {
                            type = "button",
                            style = "link",
                            action = new
                            {
                                type = "uri",
                                label = "已經成交了！恭喜",
                                uri = detailUrl
                            }
                        },
                        new
                        {
                            type = "text",
                            text = "請更新商品狀態，避免買家撲空",
                            size = "xs",
                            color = "#888888",
                            wrap = true,
                            align = "center"
                        }
                    }
                }
            });
    }

    public LineFlexMessage BuildPurchaseRequestReminderCard(string listingTitle, Guid conversationId)
    {
        var safeListingTitle = string.IsNullOrWhiteSpace(listingTitle) ? "未命名商品" : listingTitle.Trim();
        var chatUrl = BuildUrl($"/messages/{conversationId}");
        var altText = $"交易請求提醒｜商品：{safeListingTitle}";

        return new LineFlexMessage(
            AltText: altText,
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
                            text = "交易請求提醒",
                            weight = "bold",
                            size = "lg",
                            wrap = true
                        },
                        new
                        {
                            type = "text",
                            text = "有一筆購買請求即將逾時，請盡快回覆。",
                            wrap = true,
                            size = "md"
                        },
                        new
                        {
                            type = "separator",
                            margin = "md"
                        },
                        new
                        {
                            type = "box",
                            layout = "baseline",
                            margin = "md",
                            contents = new object[]
                            {
                                new
                                {
                                    type = "text",
                                    text = "商品",
                                    size = "sm",
                                    color = "#888888",
                                    flex = 2
                                },
                                new
                                {
                                    type = "text",
                                    text = safeListingTitle,
                                    size = "sm",
                                    weight = "bold",
                                    wrap = true,
                                    flex = 5
                                }
                            }
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
                                label = "前往商品聊天室",
                                uri = chatUrl
                            }
                        }
                    }
                }
            });
    }

    public LineFlexMessage BuildBindHintCard()
    {
        return BuildCard(
            title: "尚未完成綁定",
            message: "請先到網站個人設定完成 LINE 通知綁定，才能查看個人摘要。",
            buttonLabel: "前往個人設定",
            buttonUrl: BuildUrl("/account"));
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
                width = "56px",
                height = "24px",
                cornerRadius = "999px",
                backgroundColor = "#F1F3F5",
                justifyContent = "center",
                alignItems = "center",
                contents = new object[]
                {
                    new
                    {
                        type = "text",
                        text = "未讀 0",
                        size = "xxs",
                        color = "#9AA0A6",
                        align = "center"
                    }
                }
            };
        }

        var badgeText = unreadCount > 99 ? "未讀 99+" : $"未讀 {unreadCount}";
        return new
        {
            type = "box",
            layout = "vertical",
            width = "56px",
            height = "24px",
            cornerRadius = "999px",
            backgroundColor = "#FFE7EB",
            justifyContent = "center",
            alignItems = "center",
            contents = new object[]
            {
                new
                {
                    type = "text",
                    text = badgeText,
                    size = "xxs",
                    weight = "bold",
                    color = "#D90429",
                    align = "center",
                    wrap = false
                }
            }
        };
    }

    private string BuildUrl(string path) => LineLiffUrlBuilder.BuildLineOpenUrl(_options, path);
}

public sealed record LineFlexMessage(string AltText, object Contents);
