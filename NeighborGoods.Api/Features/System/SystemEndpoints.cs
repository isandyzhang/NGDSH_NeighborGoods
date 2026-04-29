using Microsoft.Extensions.Diagnostics.HealthChecks;
using NeighborGoods.Api.Shared.ApiContracts;

namespace NeighborGoods.Api.Features.System;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", () => Results.Redirect("/health"));

        app.MapGet("/health", async (HealthCheckService healthCheckService, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var report = await healthCheckService.CheckHealthAsync(cancellationToken);
            var payload = new
            {
                status = report.Status.ToString().ToLowerInvariant(),
                service = "NeighborGoods.Api",
                checks = report.Entries.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new
                    {
                        status = kvp.Value.Status.ToString().ToLowerInvariant(),
                        description = kvp.Value.Description
                    })
            };

            var response = ApiResponseFactory.Success(payload, httpContext);
            return report.Status == HealthStatus.Healthy
                ? Results.Ok(response)
                : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .WithName("HealthCheck");

        app.MapGet("/api/v1/ping", (HttpContext httpContext) =>
        {
            var payload = new
            {
                message = "pong",
                version = "v1"
            };

            return Results.Ok(ApiResponseFactory.Success(payload, httpContext));
        })
        .WithName("PingV1");

        return app;
    }
}
