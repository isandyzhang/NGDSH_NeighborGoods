using NeighborGoods.Data.Announcements;

namespace NeighborGoods.Api.Features.Announcements.Services;

internal static class AnnouncementValidation
{
    internal sealed record ValidationResult(bool IsValid, string? ErrorCode, string? ErrorMessage);

    internal static ValidationResult ValidateWrite(
        string message,
        byte severity,
        byte scope,
        DateTime? startsAt,
        DateTime? endsAt,
        string? linkUrl)
    {
        var trimmedMessage = message.Trim();
        if (string.IsNullOrEmpty(trimmedMessage))
        {
            return new ValidationResult(false, "VALIDATION_ERROR", "公告內容不可為空");
        }

        if (trimmedMessage.Length > 500)
        {
            return new ValidationResult(false, "VALIDATION_ERROR", "公告內容不可超過 500 字");
        }

        if (!Enum.IsDefined(typeof(AnnouncementSeverity), severity))
        {
            return new ValidationResult(false, "VALIDATION_ERROR", "無效的嚴重度");
        }

        if (!Enum.IsDefined(typeof(AnnouncementScope), scope))
        {
            return new ValidationResult(false, "VALIDATION_ERROR", "無效的顯示範圍");
        }

        if (startsAt.HasValue && endsAt.HasValue && endsAt <= startsAt)
        {
            return new ValidationResult(false, "VALIDATION_ERROR", "結束時間必須晚於開始時間");
        }

        if (!string.IsNullOrWhiteSpace(linkUrl))
        {
            if (!Uri.TryCreate(linkUrl.Trim(), UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return new ValidationResult(false, "VALIDATION_ERROR", "連結必須為 http 或 https");
            }
        }

        return new ValidationResult(true, null, null);
    }
}
