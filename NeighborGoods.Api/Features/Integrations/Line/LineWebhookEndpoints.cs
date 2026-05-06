using NeighborGoods.Api.Features.Integrations.Line.Services;
using NeighborGoods.Api.Shared.ApiContracts;
using System.Text.Json;

namespace NeighborGoods.Api.Features.Integrations.Line;

public static class LineWebhookEndpoints
{
    public static IEndpointRouteBuilder MapLineWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/integrations/line/webhook", async (
            HttpContext httpContext,
            LineWebhookService webhookService,
            ILoggerFactory loggerFactory,
            CancellationToken ct = default) =>
        {
            var logger = loggerFactory.CreateLogger("LineWebhook");
            using var reader = new StreamReader(httpContext.Request.Body);
            var body = await reader.ReadToEndAsync(ct);
            var signature = httpContext.Request.Headers["X-Line-Signature"].ToString();

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
                httpContext.Request.Path.Value,
                eventCount,
                firstType ?? "(null)",
                firstUserIdSuffix ?? "(null)",
                !string.IsNullOrWhiteSpace(signature),
                signature?.Length ?? 0,
                firstReplyTokenPrefix ?? "(null)",
                firstPostbackData ?? "(null)");

            var (ok, errorCode, errorMessage) = await webhookService.ProcessAsync(body, signature, ct);
            if (!ok)
            {
                logger.LogWarning(
                    "LINE webhook processing failed: errorCode={ErrorCode} errorMessage={ErrorMessage}",
                    errorCode ?? "(null)",
                    errorMessage ?? "(null)");
                if (errorCode == "LINE_WEBHOOK_SIGNATURE_INVALID")
                {
                    return Results.Unauthorized();
                }

                return Results.Json(
                    ApiResponseFactory.Error(errorCode ?? "LINE_WEBHOOK_FAILED", errorMessage ?? "LINE webhook 處理失敗", httpContext),
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            return Results.Ok();
        })
        .WithName("LineWebhookV1");

        return app;
    }
}
