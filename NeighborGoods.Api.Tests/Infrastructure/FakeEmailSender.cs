using System.Text.RegularExpressions;
using NeighborGoods.Api.Shared.Notifications;

namespace NeighborGoods.Api.Tests;

internal sealed class FakeEmailSender : IEmailSender
{
    private static readonly Regex SixDigitsRegex = new(@"\b\d{6}\b", RegexOptions.Compiled);
    private static readonly Dictionary<string, string> CodesByEmail = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<SentEmail> SentEmails = [];
    private static readonly object SyncRoot = new();

    public Task SendAsync(
        string toEmail,
        string subject,
        string plainTextContent,
        CancellationToken cancellationToken = default)
    {
        lock (SyncRoot)
        {
            SentEmails.Add(new SentEmail(toEmail.Trim(), subject, plainTextContent));
        }

        var code = ExtractCode(plainTextContent);
        if (!string.IsNullOrWhiteSpace(code))
        {
            lock (SyncRoot)
            {
                CodesByEmail[toEmail.Trim()] = code;
            }
        }

        return Task.CompletedTask;
    }

    public static IReadOnlyList<SentEmail> GetSentEmails()
    {
        lock (SyncRoot)
        {
            return SentEmails.ToList();
        }
    }

    public static string? GetCode(string email)
    {
        lock (SyncRoot)
        {
            return CodesByEmail.TryGetValue(email.Trim(), out var code) ? code : null;
        }
    }

    public static void Reset()
    {
        lock (SyncRoot)
        {
            CodesByEmail.Clear();
            SentEmails.Clear();
        }
    }

    private static string? ExtractCode(string content)
    {
        var match = SixDigitsRegex.Match(content);
        return match.Success ? match.Value : null;
    }

    public sealed record SentEmail(string ToEmail, string Subject, string PlainTextContent);
}
