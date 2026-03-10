// REQ-SEC-001, CTL-SEC-004: Rate limiting to prevent DoS and brute-force attacks.
// VUL-007 remediation: fixed window on all API; sliding window on auth endpoints.
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace ZenoHR.Api.Security;

/// <summary>
/// Extension methods for configuring per-tenant rate limiting policies.
/// Provides three named policies: general API, auth endpoints, and payroll operations.
/// REQ-SEC-001, CTL-SEC-004
/// </summary>
public static class RateLimitingExtensions
{
    // VUL-007: Named policy identifiers
    public const string GeneralApiPolicy = "general-api";
    public const string AuthPolicy = "auth-endpoints";
    public const string PayrollPolicy = "payroll-ops";

    /// <summary>
    /// Configures per-tenant rate limiting policies to protect against DoS and credential-stuffing.
    /// REQ-SEC-001, CTL-SEC-004
    /// </summary>
    public static IServiceCollection AddZenoHrRateLimiting(this IServiceCollection services)
    {
        // VUL-007: Three-tier rate limiting — general API, auth endpoints, payroll operations
        services.AddRateLimiter(options =>
        {
            // General API: 100 req/min per tenant (sliding window)
            // Partition key: tenant_id claim (authenticated) or IP (anonymous)
            options.AddPolicy(GeneralApiPolicy, context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: context.User.FindFirst("tenant_id")?.Value
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "anonymous",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 10
                    }));

            // Auth endpoints: 10 attempts per 5 minutes (fixed window) — brute-force protection
            // Partition key: IP address only (user is not yet authenticated at login time)
            options.AddPolicy(AuthPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(5),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Payroll operations: 20 req/min per tenant (computation intensive, heavy Firestore writes)
            options.AddPolicy(PayrollPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User.FindFirst("tenant_id")?.Value
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "anonymous",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 5
                    }));

            // REQ-SEC-001: Return 429 with Retry-After header on limit exceeded
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    status = 429,
                    title = "Too Many Requests",
                    detail = "Rate limit exceeded. Please retry after 60 seconds."
                }, ct);
            };
        });

        return services;
    }
}
