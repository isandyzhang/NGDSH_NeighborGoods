using NeighborGoods.Api.Features.Integrations.Line.Services;
using NeighborGoods.Api.Shared.ApiContracts;

namespace NeighborGoods.Api.Features.Integrations.Line;

public static class LineWebhookEndpoints
{
    public static IEndpointRouteBuilder MapLineWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/integrations/line/webhook", async (
            HttpContext httpContext,
            LineWebhookService webhookService,
            CancellationToken ct = default) =>
        {
            using var reader = new StreamReader(httpContext.Request.Body);
            var body = await reader.ReadToEndAsync(ct);
            var signature = httpContext.Request.Headers["X-Line-Signature"].ToString();

            var (ok, errorCode, errorMessage) = await webhookService.ProcessAsync(body, signature, ct);
            if (!ok)
            {
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
