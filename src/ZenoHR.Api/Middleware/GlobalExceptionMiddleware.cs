// REQ-OPS-005, REQ-OPS-008: Global exception middleware — catches all unhandled exceptions and
// emits a structured log entry with correlation ID before returning a RFC 9457 ProblemDetails response.

using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace ZenoHR.Api.Middleware;

/// <summary>
/// Outermost middleware that catches all unhandled exceptions and converts them to structured
/// <see href="https://datatracker.ietf.org/doc/html/rfc9457">RFC 9457 ProblemDetails</see> responses.
/// <para>
/// Three categories handled:
/// <list type="table">
///   <item><term><see cref="ValidationException"/></term><description>HTTP 422 — client-fixable input errors.</description></item>
///   <item><term><see cref="OperationCanceledException"/></term><description>HTTP 499 — client closed the connection.</description></item>
///   <item><term>All others</term><description>HTTP 500 — unexpected server fault, logged at Error with full stack trace.</description></item>
/// </list>
/// </para>
/// Register as the second middleware (after <c>UseCorrelationId</c>) so the correlation ID is
/// available for all exception log entries.
/// </summary>
internal sealed partial class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var requestPath = context.Request.Path.Value ?? "/";
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            var correlationId = GetCorrelationId(context);
            LogRequestCancelled(logger, context.Request.Method, requestPath, correlationId);
            // 499 is a non-standard but widely understood "client closed request" status.
            context.Response.StatusCode = 499;
        }
        catch (ValidationException ex)
        {
            var correlationId = GetCorrelationId(context);
            LogValidationException(logger, context.Request.Method, requestPath, correlationId,
                ex.Errors.Count());

            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.21",
                Title = "Validation failed",
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = "One or more validation errors occurred.",
                Extensions =
                {
                    ["correlationId"] = correlationId,
                    ["errors"] = ex.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage).ToArray())
                }
            };

            await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
        }
        catch (Exception ex)
        {
            var correlationId = GetCorrelationId(context);
            LogUnhandledException(logger, ex, ex.GetType().Name,
                context.Request.Method, requestPath, correlationId);

            if (context.Response.HasStarted)
                return; // Cannot modify response — let it go.

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var traceId = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;

            var problem = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                Title = "An unexpected error occurred",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "An internal server error occurred. Please contact support if this persists.",
                Extensions =
                {
                    ["correlationId"] = correlationId,
                    ["traceId"] = traceId
                }
            };

            await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
        }
    }

    private static string GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var id) && id is string s
            ? s
            : context.TraceIdentifier;

    [LoggerMessage(EventId = 9001, Level = LogLevel.Warning,
        Message = "Validation exception on {Method} {Path} CorrelationId={CorrelationId} ErrorCount={ErrorCount}")]
    private static partial void LogValidationException(
        ILogger logger, string method, string path, string correlationId, int errorCount);

    [LoggerMessage(EventId = 9002, Level = LogLevel.Information,
        Message = "Request cancelled {Method} {Path} CorrelationId={CorrelationId}")]
    private static partial void LogRequestCancelled(
        ILogger logger, string method, string path, string correlationId);

    [LoggerMessage(EventId = 9003, Level = LogLevel.Error,
        Message = "Unhandled {ExceptionType} on {Method} {Path} CorrelationId={CorrelationId}")]
    private static partial void LogUnhandledException(
        ILogger logger, Exception ex, string exceptionType, string method, string path, string correlationId);
}

/// <summary>Extension methods for registering <see cref="GlobalExceptionMiddleware"/>.</summary>
public static class GlobalExceptionMiddlewareExtensions
{
    /// <summary>Adds global unhandled exception handling to the request pipeline.</summary>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app) =>
        app.UseMiddleware<GlobalExceptionMiddleware>();
}
