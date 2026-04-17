using FileSanitizerService.Core.Exceptions;
using System.Text.Json;

namespace FileSanitizerService.Api.Middlewares;

public sealed class GlobalExceptionHandlingMiddleware
{
    private const string ProblemJsonContentType = "application/problem+json";
    private const string ProblemType = "about:blank";

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
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var response = context.Response;
        if (response.HasStarted)
        {
            _logger.LogWarning(ex, "Response has already started. Cannot write error payload.");
            return Task.CompletedTask;
        }

        var (statusCode, title, detail, logLevel) = MapException(ex);
        response.StatusCode = statusCode;
        response.ContentType = ProblemJsonContentType;

        _logger.Log(logLevel, ex,
            "HTTP {StatusCode} - {ExceptionType}: {Detail} for {Method} {Path} (TraceId: {TraceId})",
            statusCode, ex.GetType().Name, detail,
            context.Request.Method, context.Request.Path, context.TraceIdentifier);

        var payload = new ProblemPayload(ProblemType, title, statusCode, detail, context.TraceIdentifier);

        return response.WriteAsync(JsonSerializer.Serialize(payload));
    }

    private static (int StatusCode, string Title, string Detail, LogLevel LogLevel) MapException(Exception ex) => ex switch
    {
        UnsupportedFormatException => (StatusCodes.Status400BadRequest, "Bad Request", ex.Message, LogLevel.Warning),
        InvalidFileStructureException => (StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", ex.Message, LogLevel.Warning),
        BadHttpRequestException => (StatusCodes.Status400BadRequest, "Bad Request", "Invalid HTTP request.", LogLevel.Warning),
        OperationCanceledException => (499, "Client Closed Request", "Request was canceled.", LogLevel.Information),
        _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "Unexpected error.", LogLevel.Error),
    };

    private readonly record struct ProblemPayload(
        string Type,
        string Title,
        int Status,
        string Detail,
        string TraceId);
}
