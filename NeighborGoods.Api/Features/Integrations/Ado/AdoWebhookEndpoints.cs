namespace NeighborGoods.Api.Features.Integrations.Ado;

public static class AdoWebhookEndpoints
{
    public static IEndpointRouteBuilder MapAdoWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/integrations/ado/webhook", async (
            HttpContext httpContext,
            AdoWebhookMemoryStore store,
            ILoggerFactory loggerFactory,
            CancellationToken ct = default) =>
        {
            var logger = loggerFactory.CreateLogger("AdoWebhook");
            using var reader = new StreamReader(httpContext.Request.Body);
            var body = await reader.ReadToEndAsync(ct);
            var eventId = store.Add(body);

            logger.LogInformation(
                "ADO webhook received: id={EventId} length={BodyLength}",
                eventId,
                body.Length);

            return Results.Ok();
        })
        .WithName("AdoWebhookV1");

        return app;
    }
}
