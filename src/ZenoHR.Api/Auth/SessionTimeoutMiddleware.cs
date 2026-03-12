// REQ-SEC-004: Session timeout middleware — enforces idle timeout per VUL-013.
// Privileged endpoints (payroll, compliance, settings, e-filing) timeout after 15 minutes idle.
// Standard endpoints timeout after 60 minutes idle.
// Returns 401 with "session_expired" reason when the idle threshold is exceeded.

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace ZenoHR.Api.Auth;

/// <summary>
/// ASP.NET Core middleware that enforces idle session timeouts.
/// Must be registered after <c>UseAuthentication()</c> and <c>UseAuthorization()</c>
/// so that the <see cref="ClaimsPrincipal"/> is populated.
/// </summary>
/// <remarks>
/// On every authenticated request:
/// <list type="number">
/// <item>Reads the user's last activity timestamp from <see cref="SessionActivityTracker"/>.</item>
/// <item>If the idle time exceeds the applicable timeout (15 min privileged, 60 min standard),
///        returns HTTP 401 with a JSON body containing <c>"reason": "session_expired"</c>.</item>
/// <item>Otherwise, records the current UTC time as the latest activity and continues the pipeline.</item>
/// </list>
/// Anonymous or unauthenticated requests pass through without session checks.
/// </remarks>
public sealed class SessionTimeoutMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initialises a new instance of <see cref="SessionTimeoutMiddleware"/>.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public SessionTimeoutMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    /// <summary>
    /// Processes the HTTP request, enforcing idle session timeout when the user is authenticated.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, SessionActivityTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tracker);

        // Skip session checks for unauthenticated requests (login, health, etc.)
        if (context.User.Identity is not { IsAuthenticated: true })
        {
            await _next(context);
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? context.User.FindFirstValue("sub");

        // If we cannot identify the user, let the request through (auth middleware handles denial)
        if (string.IsNullOrEmpty(userId))
        {
            await _next(context);
            return;
        }

        var requestPath = context.Request.Path.Value ?? string.Empty;
        var isPrivileged = SessionPolicy.IsPrivilegedEndpoint(requestPath);
        var lastActivity = tracker.GetLastActivity(userId);

        // If there is a previous activity record, check for idle timeout
        if (lastActivity.HasValue && SessionPolicy.IsSessionExpired(lastActivity.Value, isPrivileged))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";

            var body = JsonSerializer.Serialize(new
            {
                error = "session_expired",
                reason = isPrivileged
                    ? $"Privileged session idle timeout exceeded ({SessionPolicy.PrivilegedIdleTimeoutMinutes} minutes)"
                    : $"Session idle timeout exceeded ({SessionPolicy.StandardIdleTimeoutMinutes} minutes)",
                privileged_endpoint = isPrivileged,
            });

            await context.Response.WriteAsync(body);
            return;
        }

        // Record current activity
        tracker.RecordActivity(userId, DateTimeOffset.UtcNow);

        await _next(context);
    }
}

/// <summary>
/// Extension method to register <see cref="SessionTimeoutMiddleware"/> in the HTTP pipeline.
/// </summary>
public static class SessionTimeoutMiddlewareExtensions
{
    /// <summary>
    /// Adds the session timeout middleware to the application pipeline.
    /// Must be called after <c>UseAuthentication()</c> and <c>UseAuthorization()</c>.
    /// </summary>
    // REQ-SEC-004: Idle session enforcement — closes VUL-013.
    public static IApplicationBuilder UseSessionTimeout(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SessionTimeoutMiddleware>();
    }
}
