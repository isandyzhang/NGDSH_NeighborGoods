using System.Text;
using NeighborGoods.Workers.Line;

namespace NeighborGoods.Api.Features.Integrations.Line;

public static class LineWebhookEndpoints
{
    public static IEndpointRouteBuilder MapLineWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/integrations/line/webhook", async (
            HttpContext httpContext,
            LineWebhookReplyService webhookReplyService,
            CancellationToken ct = default) =>
        {
            string body;
            using (var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync(ct);
            }

            var signature = httpContext.Request.Headers["X-Line-Signature"].FirstOrDefault();
            var (ok, errorCode, _) = await webhookReplyService.ProcessAsync(body, signature, ct);
            if (!ok && errorCode == "LINE_WEBHOOK_SIGNATURE_INVALID")
            {
                return Results.Unauthorized();
            }

            if (!ok)
            {
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Results.Ok();
        })
        .WithName("LineWebhookV1")
        .AllowAnonymous();

        return app;
    }
}
