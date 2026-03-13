// VUL-008: Manager department scoping enforcement at API middleware layer.
// REQ-SEC-002: PRD-15 §1.7 — Validates that Manager query parameters include only allowed department IDs.
// Returns 403 Forbidden if a Manager attempts cross-department access.

using System.Security.Claims;
using ZenoHR.Domain.Common;

namespace ZenoHR.Api.Auth;

/// <summary>
/// Middleware that enforces department scoping for Manager-accessible endpoints.
/// <para>
/// For Manager-role users accessing <c>/api/employees</c>, <c>/api/leave</c>, or <c>/api/timesheets</c>,
/// validates that any <c>department_id</c> query parameter value is within the user's allowed department scope.
/// Returns <c>403 Forbidden</c> if the Manager tries to access another department's data.
/// </para>
/// <para>
/// Director, HRManager, and SaasAdmin pass through without department validation.
/// </para>
/// </summary>
/// <remarks>
/// VUL-008: This middleware enforces department scoping at the API layer — complementing Firestore security rules.
/// </remarks>
public sealed class DepartmentScopeMiddleware
{
    private readonly RequestDelegate _next;

    // VUL-008: Endpoints where Manager department scoping is enforced.
    private static readonly string[] ScopedEndpointPrefixes =
    [
        "/api/employees",
        "/api/leave",
        "/api/timesheets",
    ];

    public DepartmentScopeMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    // VUL-008, REQ-SEC-002
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var user = context.User;

        // Only enforce on authenticated requests
        if (user.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;

        // Only enforce on Manager-accessible endpoints
        if (!IsScopedEndpoint(path))
        {
            await _next(context);
            return;
        }

        var roleClaim = user.FindFirstValue(ClaimTypes.Role);

        if (string.IsNullOrWhiteSpace(roleClaim)
            || !Enum.TryParse<SystemRole>(roleClaim, ignoreCase: true, out var role))
        {
            await _next(context);
            return;
        }

        // Director, HRManager, and SaasAdmin pass through — no department restriction. // REQ-SEC-002
        if (role is SystemRole.Director or SystemRole.HRManager or SystemRole.SaasAdmin)
        {
            await _next(context);
            return;
        }

        // Manager — validate department_id query parameter(s) against allowed scope.
        if (role == SystemRole.Manager)
        {
            var allowedDeptIds = user.FindAll(ZenoHrClaimNames.DeptId)
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Check department_id query parameter(s) — reject if outside scope. // VUL-008
            var requestedDeptIds = context.Request.Query["department_id"];

            foreach (var requestedDeptId in requestedDeptIds)
            {
                if (!string.IsNullOrWhiteSpace(requestedDeptId)
                    && !allowedDeptIds.Contains(requestedDeptId))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/problem+json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        type = "https://zenohr.zenowethu.co.za/errors/department-scope-violation",
                        title = "Department scope violation",
                        status = 403,
                        detail = "You do not have access to the requested department.",
                    });
                    return;
                }
            }
        }

        await _next(context);
    }

    /// <summary>
    /// Determines whether the given request path is a Manager-scoped endpoint.
    /// </summary>
    internal static bool IsScopedEndpoint(string path)
    {
        foreach (var prefix in ScopedEndpointPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

/// <summary>
/// Extension method for registering <see cref="DepartmentScopeMiddleware"/> in the pipeline.
/// </summary>
// VUL-008
public static class DepartmentScopeMiddlewareExtensions
{
    /// <summary>
    /// Adds department scope enforcement middleware to the pipeline.
    /// Should be placed after authentication and authorization middleware.
    /// </summary>
    public static IApplicationBuilder UseDepartmentScopeEnforcement(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<DepartmentScopeMiddleware>();
    }
}
