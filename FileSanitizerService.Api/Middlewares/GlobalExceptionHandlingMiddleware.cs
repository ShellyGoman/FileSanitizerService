using System.Text.Json;

namespace FileSanitizerService.Api.Middlewares;

public sealed class GlobalExceptionHandlingMiddleware(RequestDelegate next,
    ILogger<GlobalExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var (statusCode, logLevel, customLogMessage) = ex switch
            {
                ArgumentException => (400, LogLevel.Warning, "Bad request validation failed."),
                InvalidOperationException => (422, LogLevel.Warning, "Unprocessable content for sanitization."),
                NotSupportedException => (415, LogLevel.Warning, "Unsupported file format or operation."),
                UnauthorizedAccessException => (403, LogLevel.Warning, "Access denied while processing request."),
                _ => (500, LogLevel.Error, "Unexpected server error.")
            };

            logger.Log(
                logLevel,
                ex,
                "HTTP {StatusCode} - {CustomLog}. Exception: {ExceptionMessage}. {ExceptionType} for {Method} {Path}",
                statusCode,
                customLogMessage,
                ex.Message,
                ex.GetType().Name,
                context.Request.Method,
                context.Request.Path);
            await WriteErrorResponseAsync(context, statusCode, ex.Message);
        }
    }

    private static Task WriteErrorResponseAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new { status = statusCode, error = message });

        return context.Response.WriteAsync(body);
    }
}
