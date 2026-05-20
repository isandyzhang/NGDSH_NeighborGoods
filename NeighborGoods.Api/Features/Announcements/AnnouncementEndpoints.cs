using NeighborGoods.Api.Features.Announcements.Contracts;
using NeighborGoods.Api.Features.Announcements.Services;
using NeighborGoods.Api.Shared.ApiContracts;

namespace NeighborGoods.Api.Features.Announcements;

public static class AnnouncementEndpoints
{
    public static IEndpointRouteBuilder MapAnnouncementEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/announcements/active", GetActiveAnnouncementsAsync)
            .WithName("GetActiveAnnouncementsV1");

        return app;
    }

    private static async Task<IResult> GetActiveAnnouncementsAsync(
        HttpContext httpContext,
        AnnouncementQueryService queryService,
        byte? scope,
        CancellationToken ct = default)
    {
        var items = await queryService.GetActiveAsync(scope, ct);
        var payload = new ActiveAnnouncementsResponse(items);
        httpContext.Response.Headers.CacheControl = "public, max-age=30";
        return Results.Ok(ApiResponseFactory.Success(payload, httpContext));
    }
}
