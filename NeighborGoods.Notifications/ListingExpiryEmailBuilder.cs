namespace NeighborGoods.Notifications;

public sealed record ListingExpiryEmailItem(
    Guid ListingId,
    string Title,
    string? CoverImageUrl);

public static class ListingExpiryEmailBuilder
{
    private const string SoldReminder = "請更新商品狀態，避免買家撲空。";

    public static (string Subject, string PlainText, string Html) Build(
        string webBaseUrl,
        IReadOnlyList<ListingExpiryEmailItem> items)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("At least one listing is required.", nameof(items));
        }

        var baseUrl = string.IsNullOrWhiteSpace(webBaseUrl)
            ? "https://www.neighborgoodstw.com"
            : webBaseUrl.TrimEnd('/');

        var subject = items.Count == 1
            ? $"NeighborGoods｜「{items[0].Title}」刊登已滿 14 天，請延續刊登"
            : $"NeighborGoods｜您有 {items.Count} 件商品刊登已滿 14 天";

        var plainLines = new List<string>
        {
            "您好，",
            "",
            "以下商品已刊登滿 14 天，系統已改為非活躍狀態。請延續刊登以重新曝光，或若已成交請更新狀態。",
            SoldReminder,
            ""
        };

        foreach (var item in items)
        {
            var detailUrl = $"{baseUrl}/listings/{item.ListingId}";
            plainLines.Add($"・{item.Title}");
            plainLines.Add($"  延續或更新狀態：{detailUrl}");
            plainLines.Add("");
        }

        plainLines.Add("NeighborGoods");

        var plainText = string.Join('\n', plainLines);

        var itemHtmlBlocks = string.Join(
            "\n",
            items.Select(item => BuildItemHtmlBlock(baseUrl, item)));

        var html = $"""
            <!DOCTYPE html>
            <html>
            <body style="margin:0;padding:0;background:#f5f5f5;font-family:sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;margin:24px auto;background:#ffffff;border-radius:8px;overflow:hidden;">
                <tr>
                  <td style="padding:24px 24px 8px;font-size:20px;font-weight:bold;color:#111;">刊登到期通知</td>
                </tr>
                <tr>
                  <td style="padding:0 24px 16px;font-size:15px;color:#444;line-height:1.6;">
                    以下商品已刊登滿 14 天，目前為<strong>非活躍</strong>狀態。請延續刊登以重新曝光，或若已成交請更新狀態。<br/>
                    <span style="font-size:13px;color:#888;">{SoldReminder}</span>
                  </td>
                </tr>
                {itemHtmlBlocks}
                <tr>
                  <td style="padding:16px 24px 24px;font-size:12px;color:#aaa;">NeighborGoods</td>
                </tr>
              </table>
            </body>
            </html>
            """;

        return (subject, plainText, html);
    }

    private static string BuildItemHtmlBlock(string baseUrl, ListingExpiryEmailItem item)
    {
        var detailUrl = $"{baseUrl}/listings/{item.ListingId}";
        var safeTitle = System.Net.WebUtility.HtmlEncode(item.Title);
        var imageBlock = string.IsNullOrWhiteSpace(item.CoverImageUrl)
            ? string.Empty
            : $"""
                <tr>
                  <td style="padding:0 24px 12px;">
                    <img src="{System.Net.WebUtility.HtmlEncode(item.CoverImageUrl)}" alt="" style="width:100%;max-width:552px;border-radius:8px;display:block;" />
                  </td>
                </tr>
                """;

        return $"""
            {imageBlock}
            <tr>
              <td style="padding:8px 24px 4px;font-size:17px;font-weight:bold;color:#111;">{safeTitle}</td>
            </tr>
            <tr>
              <td style="padding:0 24px 20px;">
                <a href="{detailUrl}" style="display:inline-block;background:#1DB446;color:#fff;text-decoration:none;padding:12px 20px;border-radius:6px;font-size:15px;font-weight:bold;margin-right:8px;margin-bottom:8px;">延續刊登商品</a>
                <a href="{detailUrl}" style="display:inline-block;background:#f0f0f0;color:#333;text-decoration:none;padding:12px 20px;border-radius:6px;font-size:15px;margin-bottom:8px;">已經成交了！恭喜</a>
                <div style="font-size:12px;color:#888;margin-top:6px;">{SoldReminder}</div>
              </td>
            </tr>
            """;
    }
}
