using Microsoft.AspNetCore.Diagnostics;
using NeighborGoods.Api.Features.Listing;

namespace NeighborGoods.Api.Shared.ApiContracts;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", httpContext.TraceIdentifier);

        var (statusCode, code, message) = exception switch
        {
            ListingAccessException listingAccessException => (
                listingAccessException.StatusCode,
                listingAccessException.Code,
                listingAccessException.Message),
            ArgumentException argumentException => (
                StatusCodes.Status400BadRequest,
                "VALIDATION_ERROR",
                argumentException.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                "INTERNAL_SERVER_ERROR",
                "系統發生未預期錯誤，請稍後再試")
        };

        var details = environment.IsDevelopment()
            ? new
            {
                exceptionType = exception.GetType().Name,
                exceptionMessage = exception.Message
            }
            : null;

        var error = ApiResponseFactory.Error(code, message, httpContext, details);
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(error, cancellationToken);
        return true;
    }
}
