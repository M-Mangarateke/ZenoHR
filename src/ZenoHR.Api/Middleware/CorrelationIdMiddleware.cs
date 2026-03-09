// REQ-OPS-005, REQ-OPS-008: Correlation ID middleware — assigns a unique request ID to every
// inbound HTTP request so that all log entries within a request can be correlated in Azure Monitor.

using Microsoft.Extensions.Primitives;

namespace ZenoHR.Api.Middleware;

/// <summary>
/// Assigns a correlation ID to every request:
/// <list type="bullet">
///   <item>Reads <c>X-Correlation-Id</c> from request headers if present; otherwise generates a new <see cref="Guid"/>.</item>
///   <item>Stores the value in <see cref="HttpContext.Items"/> under the key <c>"CorrelationId"</c>.</item>
///   <item>Echoes the value back in the response <c>X-Correlation-Id</c> header.</item>
///   <item>Opens a log scope so all log entries for this request automatically include <c>CorrelationId</c>.</item>
/// </list>
/// Register before <c>UseGlobalExceptionHandler</c> and all other middleware so every layer has access.
/// </summary>
internal sealed partial class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    internal const string HeaderName = "X-Correlation-Id";
    internal const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        // Accept a client-supplied correlation ID or generate a new one.
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out StringValues values)
            && !StringValues.IsNullOrEmpty(values)
            ? values.ToString()
            : Guid.NewGuid().ToString("D");

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        var path = context.Request.Path.Value ?? "/";

        // Open a log scope — all log entries within this request will include CorrelationId.
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            LogRequest(logger, context.Request.Method, path, correlationId);
            await next(context);
        }
    }

    [LoggerMessage(EventId = 9000, Level = LogLevel.Debug,
        Message = "Request {Method} {Path} CorrelationId={CorrelationId}")]
    private static partial void LogRequest(
        ILogger logger, string method, string path, string correlationId);
}

/// <summary>Extension methods for registering <see cref="CorrelationIdMiddleware"/>.</summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>Adds correlation ID tracking to the request pipeline.</summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();
}
