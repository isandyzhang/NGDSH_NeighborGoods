using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NeighborGoods.Workers.Line;

namespace NeighborGoods.Functions;

public sealed class LineWebhookTrigger(
    LineWebhookReplyService webhookReplyService,
    ILogger<LineWebhookTrigger> logger)
{
    [Function(nameof(LineWebhook))]
    public async Task<HttpResponseData> LineWebhook(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "line/webhook")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(request, cancellationToken);
        var signature = request.Headers.TryGetValues("X-Line-Signature", out var values)
            ? values.FirstOrDefault()
            : null;

        LogDiagnosticHit(request.Url.AbsolutePath, body, signature);

        var (ok, errorCode, errorMessage) = await webhookReplyService.ProcessAsync(body, signature, cancellationToken);
        if (!ok)
        {
            logger.LogWarning(
                "LINE webhook processing failed: errorCode={ErrorCode} errorMessage={ErrorMessage}",
                errorCode ?? "(null)",
                errorMessage ?? "(null)");

            if (errorCode == "LINE_WEBHOOK_SIGNATURE_INVALID")
            {
                return request.CreateResponse(HttpStatusCode.Unauthorized);
            }

            var failure = request.CreateResponse(HttpStatusCode.InternalServerError);
            await failure.WriteStringAsync(errorMessage ?? "LINE webhook 處理失敗", cancellationToken);
            return failure;
        }

        return request.CreateResponse(HttpStatusCode.OK);
    }

    private void LogDiagnosticHit(string path, string body, string? signature)
    {
        var eventCount = 0;
        string? firstType = null;
        string? firstUserIdSuffix = null;
        string? firstReplyTokenPrefix = null;
        string? firstPostbackData = null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("events", out var eventsElement)
                && eventsElement.ValueKind == JsonValueKind.Array)
            {
                eventCount = eventsElement.GetArrayLength();
                if (eventCount > 0)
                {
                    var first = eventsElement[0];
                    firstType = first.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;

                    if (first.TryGetProperty("source", out var sourceEl)
                        && sourceEl.TryGetProperty("userId", out var uidEl))
                    {
                        var uid = uidEl.GetString() ?? string.Empty;
                        firstUserIdSuffix = uid.Length <= 6 ? uid : uid[^6..];
                    }

                    firstReplyTokenPrefix = first.TryGetProperty("replyToken", out var rtEl)
                        ? (rtEl.GetString() ?? string.Empty) is var rt && rt.Length > 0
                            ? (rt.Length <= 6 ? rt : rt[..6])
                            : null
                        : null;

                    if (first.TryGetProperty("postback", out var postbackEl)
                        && postbackEl.TryGetProperty("data", out var dataEl))
                    {
                        firstPostbackData = dataEl.GetString();
                    }
                }
            }
        }
        catch
        {
            // Keep webhook robust even if diagnostic parsing fails.
        }

        logger.LogInformation(
            "LINE webhook hit: path={Path} eventCount={EventCount} firstType={FirstType} firstUserIdSuffix={UserIdSuffix} hasSignature={HasSignature} signatureLength={SignatureLength} firstReplyTokenPrefix={ReplyTokenPrefix} firstPostbackData={PostbackData}",
            path,
            eventCount,
            firstType ?? "(null)",
            firstUserIdSuffix ?? "(null)",
            !string.IsNullOrWhiteSpace(signature),
            signature?.Length ?? 0,
            firstReplyTokenPrefix ?? "(null)",
            firstPostbackData ?? "(null)");
    }

    private static async Task<string> ReadBodyAsync(HttpRequestData request, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
