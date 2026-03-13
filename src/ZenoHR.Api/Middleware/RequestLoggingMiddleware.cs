// REQ-OPS-005, REQ-OPS-008: Structured request/response logging middleware.
// VUL-027: Closes missing request/response logging (MEDIUM severity).
// CTL-POPIA-001: Does NOT log request/response bodies — only method, path, status, duration.
// POPIA compliance: no PII is captured in request logs.

using System.Diagnostics;

namespace ZenoHR.Api.Middleware;

/// <summary>
/// Lightweight middleware that logs every HTTP request with structured fields:
/// method, path, status code, and duration in milliseconds.
/// <para>
/// CTL-POPIA-001: Request/response bodies are deliberately excluded to prevent
/// accidental PII leakage into log aggregators (Azure Monitor, Application Insights).
/// </para>
/// </summary>
internal sealed partial class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            // REQ-OPS-005: Structured logging for observability — parseable by Azure Monitor / Log Analytics.
            // CTL-POPIA-001: Only method, path, status, duration — no bodies, no headers, no PII.
            if (statusCode >= 500)
            {
                LogServerError(logger, method, path, statusCode, elapsedMs);
            }
            else if (statusCode >= 400)
            {
                LogClientError(logger, method, path, statusCode, elapsedMs);
            }
            else
            {
                LogSuccess(logger, method, path, statusCode, elapsedMs);
            }
        }
    }

    [LoggerMessage(EventId = 9100, Level = LogLevel.Information,
        Message = "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:F1}ms")]
    private static partial void LogSuccess(
        ILogger logger, string method, string path, int statusCode, double elapsedMs);

    [LoggerMessage(EventId = 9101, Level = LogLevel.Warning,
        Message = "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:F1}ms")]
    private static partial void LogClientError(
        ILogger logger, string method, string path, int statusCode, double elapsedMs);

    [LoggerMessage(EventId = 9102, Level = LogLevel.Error,
        Message = "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:F1}ms")]
    private static partial void LogServerError(
        ILogger logger, string method, string path, int statusCode, double elapsedMs);
}

/// <summary>Extension methods for registering <see cref="RequestLoggingMiddleware"/>.</summary>
// REQ-OPS-005: Register in Program.cs pipeline after correlation ID, before exception handler.
public static class RequestLoggingMiddlewareExtensions
{
    /// <summary>Adds structured request logging to the request pipeline.</summary>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestLoggingMiddleware>();
}
