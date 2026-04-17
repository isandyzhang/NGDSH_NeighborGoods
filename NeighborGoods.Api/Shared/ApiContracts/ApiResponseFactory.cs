namespace NeighborGoods.Api.Shared.ApiContracts;

public static class ApiResponseFactory
{
    public static ApiSuccessResponse<T> Success<T>(T data, HttpContext httpContext)
    {
        return new ApiSuccessResponse<T>(
            true,
            data,
            new ApiMeta(httpContext.TraceIdentifier, DateTime.UtcNow)
        );
    }

    public static ApiErrorResponse Error(
        string code,
        string message,
        HttpContext httpContext,
        object? details = null)
    {
        return new ApiErrorResponse(
            false,
            new ApiErrorBody(code, message, details),
            new ApiMeta(httpContext.TraceIdentifier, DateTime.UtcNow)
        );
    }
}
