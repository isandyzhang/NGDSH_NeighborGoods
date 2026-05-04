using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
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
        var (statusCode, code, message) = MapException(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogWarning(exception, "Request failed with {StatusCode}. TraceId={TraceId}", statusCode, httpContext.TraceIdentifier);
        }

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

    private static (int StatusCode, string Code, string Message) MapException(Exception exception) =>
        exception switch
        {
            ListingAccessException listingAccessException => (
                listingAccessException.StatusCode,
                listingAccessException.Code,
                listingAccessException.Message),
            ArgumentException argumentException => (
                StatusCodes.Status400BadRequest,
                "VALIDATION_ERROR",
                argumentException.Message),
            UnauthorizedAccessException unauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "UNAUTHORIZED",
                string.IsNullOrWhiteSpace(unauthorizedAccessException.Message)
                    ? "需要登入"
                    : unauthorizedAccessException.Message),
            InvalidOperationException { Message: var msg }
                when msg.Contains("Line OAuth settings are incomplete", StringComparison.Ordinal) => (
                StatusCodes.Status503ServiceUnavailable,
                "LINE_OAUTH_MISCONFIGURED",
                "LINE 登入尚未正確設定（ChannelId／ChannelSecret／CallbackUrl）。請確認 Container App 環境變數 Line__* 或對應的 Secret。"),
            DbUpdateException dbUpdateException when IsSqlUniqueConstraintViolation(dbUpdateException) => (
                StatusCodes.Status409Conflict,
                "DATABASE_CONFLICT",
                "資料與目前狀態衝突（例如重複送出），請重新整理後再試。"),
            DbUpdateException => (
                StatusCodes.Status400BadRequest,
                "DATABASE_ERROR",
                "資料更新失敗，請確認輸入或稍後再試。"),
            _ => (
                StatusCodes.Status500InternalServerError,
                "INTERNAL_SERVER_ERROR",
                "系統發生未預期錯誤，請稍後再試")
        };

    /// <summary>SQL Server: 2601/2627 為唯一索引／主鍵違規。</summary>
    private static bool IsSqlUniqueConstraintViolation(DbUpdateException ex)
    {
        for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is SqlException sql && sql.Number is 2601 or 2627)
            {
                return true;
            }
        }

        return false;
    }
}
