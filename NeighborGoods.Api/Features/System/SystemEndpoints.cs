using NeighborGoods.Api.Shared.ApiContracts;

namespace NeighborGoods.Api.Features.System;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", () => Results.Redirect("/health"));

        app.MapGet("/health", (HttpContext httpContext) =>
        {
            var payload = new
            {
                status = "ok",
                service = "NeighborGoods.Api"
            };

            return Results.Ok(ApiResponseFactory.Success(payload, httpContext));
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
