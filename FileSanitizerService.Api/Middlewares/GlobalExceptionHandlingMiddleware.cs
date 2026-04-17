using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace FileSanitizerService.Api.Middlewares;

public sealed class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex, _logger);
        }
    }

    private static Task HandleExceptionAsync(
        HttpContext context,
        Exception ex,
        ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var baseMessage = string.IsNullOrWhiteSpace(ex.Message) ? "No details." : ex.Message;
        var (statusCode, message) = ex switch
        {
            ArgumentException => (HttpStatusCode.BadRequest, baseMessage),
            InvalidOperationException => ((HttpStatusCode)StatusCodes.Status422UnprocessableEntity, baseMessage),
            NotSupportedException => (HttpStatusCode.UnsupportedMediaType, baseMessage),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, baseMessage),
            KeyNotFoundException => (HttpStatusCode.NotFound, baseMessage),
            _ => (HttpStatusCode.InternalServerError, "Unexpected server error.")
        };

        response.StatusCode = (int)statusCode;
        logger.LogWarning(
            "HTTP {StatusCode} - {ExceptionType}: {Message} for {Method} {Path}",
            response.StatusCode,
            ex.GetType().Name,
            message,
            context.Request.Method,
            context.Request.Path);

        var result = JsonSerializer.Serialize(
            new { error = message, status = response.StatusCode },
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        return response.WriteAsync(result);
    }
}
