using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NeighborGoods.Api.Features.Integrations.Ado;

public sealed partial class AdoWebhookNormalizer
{
    private static readonly HashSet<string> SnapshotSkipFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "System.Rev",
        "System.Watermark",
        "System.AuthorizedDate",
        "System.RevisedDate",
        "System.ChangedDate",
        "System.CommentCount",
        "System.BoardColumnDone",
    };

    private static readonly Dictionary<string, string> BuiltInFieldLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["System.Title"] = "標題",
        ["System.State"] = "狀態",
        ["System.Reason"] = "原因",
        ["System.AssignedTo"] = "負責人",
        ["System.WorkItemType"] = "類型",
        ["System.TeamProject"] = "專案",
        ["System.AreaPath"] = "區域路徑",
        ["System.IterationPath"] = "迭代路徑",
        ["System.Description"] = "描述",
        ["System.CreatedBy"] = "建立者",
        ["System.CreatedDate"] = "建立時間",
        ["System.ChangedBy"] = "變更者",
        ["System.BoardColumn"] = "看板欄位",
        ["Microsoft.VSTS.Common.StateChangeDate"] = "狀態變更時間",
        ["Microsoft.VSTS.Common.ActivatedDate"] = "啟用時間",
        ["Microsoft.VSTS.Common.ActivatedBy"] = "啟用者",
        ["Microsoft.VSTS.Common.StackRank"] = "堆疊排序",
    };

    public AdoWebhookNormalizationResult Normalize(string rawBody, IReadOnlyDictionary<string, string> fieldDisplayNames)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return new AdoWebhookNormalizationResult(
                null,
                null,
                null,
                null,
                null,
                "（空 payload）",
                "（空 payload）",
                "skipped",
                null);
        }

        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;
            var eventType = GetString(root, "eventType");
            var messageText = GetNestedString(root, "message", "text");

            if (!IsWorkItemEvent(eventType))
            {
                var basicSummary = BuildBasicSummary(eventType, messageText);
                return new AdoWebhookNormalizationResult(
                    eventType,
                    null,
                    null,
                    null,
                    null,
                    TruncatePreview(basicSummary),
                    basicSummary,
                    "skipped",
                    null);
            }

            var resource = root.GetProperty("resource");
            var workItemId = GetInt(resource, "workItemId");
            var revisionFields = GetObjectProperties(resource, "revision", "fields");
            var changedFields = GetChangedFields(resource);
            var workItemTitle = GetFieldString(revisionFields, "System.Title");
            var projectName = GetFieldString(revisionFields, "System.TeamProject");
            var workItemType = GetFieldString(revisionFields, "System.WorkItemType");
            var changedBy = GetNestedString(resource, "revisedBy", "displayName")
                ?? GetFieldString(revisionFields, "System.ChangedBy");
            var changedDate = GetFieldString(revisionFields, "System.ChangedDate");
            var workItemUrl = GetLinkHref(resource, "html");

            var unresolvedCustomFields = 0;
            var summaryBuilder = new StringBuilder();
            summaryBuilder.AppendLine($"事件：{eventType ?? "unknown"}");
            if (workItemId.HasValue)
            {
                summaryBuilder.AppendLine($"Work Item：#{workItemId.Value} {workItemTitle}".TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(workItemType))
            {
                summaryBuilder.AppendLine($"類型：{workItemType}");
            }

            if (!string.IsNullOrWhiteSpace(projectName))
            {
                summaryBuilder.AppendLine($"專案：{projectName}");
            }

            if (!string.IsNullOrWhiteSpace(changedBy))
            {
                summaryBuilder.AppendLine($"變更者：{changedBy}");
            }

            if (!string.IsNullOrWhiteSpace(changedDate))
            {
                summaryBuilder.AppendLine($"變更時間：{changedDate}");
            }

            if (!string.IsNullOrWhiteSpace(workItemUrl))
            {
                summaryBuilder.AppendLine($"連結：{workItemUrl}");
            }

            if (changedFields.Count > 0)
            {
                summaryBuilder.AppendLine();
                summaryBuilder.AppendLine("【本次變更】");
                foreach (var change in changedFields)
                {
                    var label = ResolveFieldLabel(change.Key, fieldDisplayNames, ref unresolvedCustomFields);
                    var oldValue = FormatFieldValue(change.OldValue);
                    var newValue = FormatFieldValue(change.NewValue);
                    if (string.IsNullOrEmpty(oldValue))
                    {
                        summaryBuilder.AppendLine($"- {label}：{newValue}");
                    }
                    else
                    {
                        summaryBuilder.AppendLine($"- {label}：{oldValue} -> {newValue}");
                    }
                }
            }

            if (revisionFields.Count > 0)
            {
                summaryBuilder.AppendLine();
                summaryBuilder.AppendLine("【欄位快照】");
                foreach (var field in OrderSnapshotFields(revisionFields))
                {
                    if (SnapshotSkipFields.Contains(field.Key))
                    {
                        continue;
                    }

                    if (IsSentinelRevisedDate(field.Key, field.Value))
                    {
                        continue;
                    }

                    var label = ResolveFieldLabel(field.Key, fieldDisplayNames, ref unresolvedCustomFields);
                    summaryBuilder.AppendLine($"- {label}：{FormatFieldValue(field.Value)}");
                }
            }

            var normalizedSummary = summaryBuilder.ToString().TrimEnd();
            var summaryPreview = workItemId.HasValue
                ? $"#{workItemId.Value} {workItemTitle}".Trim()
                : TruncatePreview(messageText ?? eventType ?? "work item 事件");

            var fieldResolveStatus = ResolveFieldStatus(fieldDisplayNames, unresolvedCustomFields, changedFields, revisionFields);

            return new AdoWebhookNormalizationResult(
                eventType,
                workItemId,
                workItemTitle,
                projectName,
                workItemUrl,
                TruncatePreview(summaryPreview),
                normalizedSummary,
                fieldResolveStatus,
                null);
        }
        catch (JsonException ex)
        {
            return new AdoWebhookNormalizationResult(
                null,
                null,
                null,
                null,
                null,
                "JSON 解析失敗",
                null,
                "failed",
                ex.Message);
        }
    }

    private static bool IsWorkItemEvent(string? eventType) =>
        !string.IsNullOrWhiteSpace(eventType)
        && eventType.StartsWith("workitem.", StringComparison.OrdinalIgnoreCase);

    private static string BuildBasicSummary(string? eventType, string? messageText)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"事件：{eventType ?? "unknown"}");
        if (!string.IsNullOrWhiteSpace(messageText))
        {
            builder.AppendLine();
            builder.AppendLine(messageText.Trim());
        }

        return builder.ToString().TrimEnd();
    }

    private static string ResolveFieldStatus(
        IReadOnlyDictionary<string, string> fieldDisplayNames,
        int unresolvedCustomFields,
        IReadOnlyList<ChangedField> changedFields,
        IReadOnlyDictionary<string, JsonElement> revisionFields)
    {
        if (fieldDisplayNames.Count == 0)
        {
            var hasCustom = changedFields.Any(x => x.Key.StartsWith("Custom.", StringComparison.OrdinalIgnoreCase))
                || revisionFields.Keys.Any(x => x.StartsWith("Custom.", StringComparison.OrdinalIgnoreCase));
            return hasCustom ? "failed" : "skipped";
        }

        if (unresolvedCustomFields > 0)
        {
            return "partial";
        }

        return "success";
    }

    private static string ResolveFieldLabel(
        string referenceName,
        IReadOnlyDictionary<string, string> fieldDisplayNames,
        ref int unresolvedCustomFields)
    {
        if (fieldDisplayNames.TryGetValue(referenceName, out var displayName)
            && !string.Equals(displayName, referenceName, StringComparison.OrdinalIgnoreCase))
        {
            return displayName;
        }

        if (BuiltInFieldLabels.TryGetValue(referenceName, out var builtInLabel))
        {
            return builtInLabel;
        }

        if (referenceName.StartsWith("Custom.", StringComparison.OrdinalIgnoreCase)
            && CustomGuidFieldRegex().IsMatch(referenceName))
        {
            unresolvedCustomFields++;
        }

        return referenceName;
    }

    private static IEnumerable<KeyValuePair<string, JsonElement>> OrderSnapshotFields(
        IReadOnlyDictionary<string, JsonElement> revisionFields)
    {
        return revisionFields
            .OrderBy(x => x.Key.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsSentinelRevisedDate(string fieldName, JsonElement value)
    {
        if (!fieldName.Equals("System.RevisedDate", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var text = FormatFieldValue(value);
        return text.StartsWith("9999", StringComparison.Ordinal);
    }

    private static List<ChangedField> GetChangedFields(JsonElement resource)
    {
        var results = new List<ChangedField>();
        if (!resource.TryGetProperty("fields", out var fieldsElement)
            || fieldsElement.ValueKind != JsonValueKind.Object)
        {
            return results;
        }

        foreach (var property in fieldsElement.EnumerateObject())
        {
            JsonElement? oldValue = null;
            JsonElement? newValue = null;
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                if (property.Value.TryGetProperty("oldValue", out var oldElement))
                {
                    oldValue = oldElement;
                }

                if (property.Value.TryGetProperty("newValue", out var newElement))
                {
                    newValue = newElement;
                }
            }

            results.Add(new ChangedField(property.Name, oldValue, newValue));
        }

        return results;
    }

    private static Dictionary<string, JsonElement> GetObjectProperties(JsonElement parent, params string[] path)
    {
        var current = parent;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            }
        }

        if (current.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }

        return current.EnumerateObject()
            .ToDictionary(x => x.Name, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static string? GetFieldString(IReadOnlyDictionary<string, JsonElement> fields, string fieldName) =>
        fields.TryGetValue(fieldName, out var value) ? FormatFieldValue(value) : null;

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) ? value.GetString() : null;

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static string? GetNestedString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
    }

    private static string? GetLinkHref(JsonElement resource, string linkName)
    {
        if (!resource.TryGetProperty("_links", out var links)
            || !links.TryGetProperty(linkName, out var link)
            || !link.TryGetProperty("href", out var href))
        {
            return null;
        }

        return href.GetString();
    }

    private static string FormatFieldValue(JsonElement? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return value.Value.ValueKind switch
        {
            JsonValueKind.String => value.Value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.Value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => value.Value.ToString(),
        };
    }

    private static string TruncatePreview(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var normalized = text.Replace("\r\n", " ").Replace('\n', ' ').Trim();
        return normalized.Length <= 80 ? normalized : normalized[..80];
    }

    [GeneratedRegex(@"^Custom\.[0-9a-fA-F-]{36}$", RegexOptions.CultureInvariant)]
    private static partial Regex CustomGuidFieldRegex();

    private sealed record ChangedField(string Key, JsonElement? OldValue, JsonElement? NewValue);
}
