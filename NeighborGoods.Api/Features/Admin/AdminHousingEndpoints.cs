using NeighborGoods.Api.Features.Admin.Contracts.Requests;
using NeighborGoods.Api.Features.Admin.Services;
using NeighborGoods.Api.Shared.ApiContracts;
using NeighborGoods.Api.Shared.Security;
using NeighborGoods.Data;

namespace NeighborGoods.Api.Features.Admin;

public static class AdminHousingEndpoints
{
    public static IEndpointRouteBuilder MapAdminHousingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/residences", ListResidencesAsync)
            .WithName("AdminListResidencesV1")
            .RequireAuthorization();
        app.MapPost("/api/v1/admin/residences", CreateResidenceAsync)
            .WithName("AdminCreateResidenceV1")
            .RequireAuthorization();
        app.MapPatch("/api/v1/admin/residences/{id:int}", UpdateResidenceAsync)
            .WithName("AdminUpdateResidenceV1")
            .RequireAuthorization();
        app.MapPatch("/api/v1/admin/residences/{id:int}/enabled", SetResidenceEnabledAsync)
            .WithName("AdminSetResidenceEnabledV1")
            .RequireAuthorization();
        app.MapDelete("/api/v1/admin/residences/{id:int}", DeleteResidenceAsync)
            .WithName("AdminDeleteResidenceV1")
            .RequireAuthorization();

        app.MapGet("/api/v1/admin/pickup-locations", ListPickupLocationsAsync)
            .WithName("AdminListPickupLocationsV1")
            .RequireAuthorization();
        app.MapPost("/api/v1/admin/pickup-locations", CreatePickupLocationAsync)
            .WithName("AdminCreatePickupLocationV1")
            .RequireAuthorization();
        app.MapPatch("/api/v1/admin/pickup-locations/{id:int}", UpdatePickupLocationAsync)
            .WithName("AdminUpdatePickupLocationV1")
            .RequireAuthorization();
        app.MapPatch("/api/v1/admin/pickup-locations/{id:int}/enabled", SetPickupLocationEnabledAsync)
            .WithName("AdminSetPickupLocationEnabledV1")
            .RequireAuthorization();
        app.MapDelete("/api/v1/admin/pickup-locations/{id:int}", DeletePickupLocationAsync)
            .WithName("AdminDeletePickupLocationV1")
            .RequireAuthorization();

        app.MapGet("/api/v1/admin/categories", ListCategoriesAsync)
            .WithName("AdminListCategoriesV1")
            .RequireAuthorization();
        app.MapPost("/api/v1/admin/categories", CreateCategoryAsync)
            .WithName("AdminCreateCategoryV1")
            .RequireAuthorization();
        app.MapPatch("/api/v1/admin/categories/{id:int}", UpdateCategoryAsync)
            .WithName("AdminUpdateCategoryV1")
            .RequireAuthorization();
        app.MapPatch("/api/v1/admin/categories/{id:int}/enabled", SetCategoryEnabledAsync)
            .WithName("AdminSetCategoryEnabledV1")
            .RequireAuthorization();
        app.MapDelete("/api/v1/admin/categories/{id:int}", DeleteCategoryAsync)
            .WithName("AdminDeleteCategoryV1")
            .RequireAuthorization();

        return app;
    }

    private static Task<IResult> ListResidencesAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminHousingService housingService,
        CancellationToken ct) =>
        ExecuteAsync(httpContext, currentUser, dbContext, () => housingService.ListResidencesAsync(ct), ct);

    private static Task<IResult> CreateResidenceAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminHousingService housingService,
        UpsertAdminResidenceRequest request,
        CancellationToken ct) =>
        ExecuteAsync(httpContext, currentUser, dbContext, () => housingService.CreateResidenceAsync(request, ct), ct);

    private static Task<IResult> UpdateResidenceAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminHousingService housingService,
        int id,
        UpsertAdminResidenceRequest request,
        CancellationToken ct) =>
        ExecuteAsync(httpContext, currentUser, dbContext, () => housingService.UpdateResidenceAsync(id, request, ct), ct);

    private static Task<IResult> SetResidenceEnabledAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminHousingService housingService,
        int id,
        SetLookupEnabledRequest request,
        CancellationToken ct) =>
        ExecuteAsync(
            httpContext,
            currentUser,
            dbContext,
            () => housingService.SetResidenceEnabledAsync(id, request.IsEnabled, ct),
            ct);

    private static Task<IResult> DeleteResidenceAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminHousingService housingService,
        int id,
        CancellationToken ct) =>
        ExecuteAsync(
            httpContext,
            currentUser,
            dbContext,
            async () =>
            {
                await housingService.DeleteResidenceAsync(id, ct);
                return new { id, deleted = true };
            },
            ct);

    private static Task<IResult> ListPickupLocationsAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminHousingService housingService,
        int? residenceId,
        bool sharedOnly = false,
        CancellationToken ct = default) =>
        ExecuteAsync(
            httpContext,
            currentUser,
            dbContext,
            () => housingService.ListPickupLocationsAsync(residenceId, sharedOnly, ct),
            ct);

    private static Task<IResult> CreatePickupLocationAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminHousingService housingService,
        UpsertAdminPickupLocationRequest request,
        CancellationToken ct) =>
        ExecuteAsync(httpContext, currentUser, dbContext, () => housingService.CreatePickupLocationAsync(request, ct), ct);

    private static Task<IResult> UpdatePickupLocationAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminHousingService housingService,
        int id,
        UpsertAdminPickupLocationRequest request,
        CancellationToken ct) =>
        ExecuteAsync(
            httpContext,
            currentUser,
            dbContext,
            () => housingService.UpdatePickupLocationAsync(id, request, ct),
            ct);

    private static Task<IResult> SetPickupLocationEnabledAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminHousingService housingService,
        int id,
        SetLookupEnabledRequest request,
        CancellationToken ct) =>
        ExecuteAsync(
            httpContext,
            currentUser,
            dbContext,
            () => housingService.SetPickupLocationEnabledAsync(id, request.IsEnabled, ct),
            ct);

    private static Task<IResult> DeletePickupLocationAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminHousingService housingService,
        int id,
        CancellationToken ct) =>
        ExecuteAsync(
            httpContext,
            currentUser,
            dbContext,
            async () =>
            {
                await housingService.DeletePickupLocationAsync(id, ct);
                return new { id, deleted = true };
            },
            ct);

    private static Task<IResult> ListCategoriesAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminHousingService housingService,
        CancellationToken ct) =>
        ExecuteAsync(httpContext, currentUser, dbContext, () => housingService.ListCategoriesAsync(ct), ct);

    private static Task<IResult> CreateCategoryAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminHousingService housingService,
        UpsertAdminCategoryRequest request,
        CancellationToken ct) =>
        ExecuteAsync(httpContext, currentUser, dbContext, () => housingService.CreateCategoryAsync(request, ct), ct);

    private static Task<IResult> UpdateCategoryAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminHousingService housingService,
        int id,
        UpsertAdminCategoryRequest request,
        CancellationToken ct) =>
        ExecuteAsync(httpContext, currentUser, dbContext, () => housingService.UpdateCategoryAsync(id, request, ct), ct);

    private static Task<IResult> SetCategoryEnabledAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminHousingService housingService,
        int id,
        SetLookupEnabledRequest request,
        CancellationToken ct) =>
        ExecuteAsync(
            httpContext,
            currentUser,
            dbContext,
            () => housingService.SetCategoryEnabledAsync(id, request.IsEnabled, ct),
            ct);

    private static Task<IResult> DeleteCategoryAsync(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        AdminHousingService housingService,
        int id,
        CancellationToken ct) =>
        ExecuteAsync(
            httpContext,
            currentUser,
            dbContext,
            async () =>
            {
                await housingService.DeleteCategoryAsync(id, ct);
                return new { id, deleted = true };
            },
            ct);

    private static async Task<IResult> ExecuteAsync<T>(
        HttpContext httpContext,
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        if (!await AdminAccess.IsAdminAsync(currentUser, dbContext, cancellationToken))
        {
            return Results.Json(
                ApiResponseFactory.Error("FORBIDDEN", "僅管理員可存取此資源", httpContext),
                statusCode: StatusCodes.Status403Forbidden);
        }

        try
        {
            var data = await action();
            return Results.Ok(ApiResponseFactory.Success(data, httpContext));
        }
        catch (AdminHousingException ex)
        {
            return Results.Json(
                ApiResponseFactory.Error(ex.Code, ex.Message, httpContext),
                statusCode: ex.StatusCode);
        }
    }
}
