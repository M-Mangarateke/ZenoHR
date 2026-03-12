// REQ-SEC-004: Session timeout policy — privileged endpoints require tighter idle timeout.
// VUL-013: Firebase Auth tokens expire at 1 hour but no idle session timeout was enforced.
// This class defines timeout constants and path classification for session enforcement.

namespace ZenoHR.Api.Auth;

/// <summary>
/// Defines session timeout constants and endpoint classification for idle session enforcement.
/// Privileged endpoints (payroll, compliance, settings, e-filing) require a 15-minute idle timeout.
/// Standard endpoints use a 60-minute idle timeout aligned with Firebase token lifetime.
/// </summary>
public sealed class SessionPolicy
{
    /// <summary>
    /// Idle timeout in minutes for privileged endpoints (payroll, compliance, settings, e-filing).
    /// After 15 minutes of inactivity, the user must re-authenticate for these operations.
    /// </summary>
    public const int PrivilegedIdleTimeoutMinutes = 15;

    /// <summary>
    /// Idle timeout in minutes for standard (non-privileged) endpoints.
    /// Aligned with Firebase Auth token lifetime (1 hour).
    /// </summary>
    public const int StandardIdleTimeoutMinutes = 60;

    /// <summary>
    /// Path prefixes that are classified as privileged and subject to the shorter idle timeout.
    /// </summary>
    private static readonly string[] PrivilegedPathPrefixes =
    [
        "/api/payroll/",
        "/api/compliance/",
        "/api/settings/",
        "/api/efiling/",
    ];

    /// <summary>
    /// Determines whether the given request path targets a privileged endpoint
    /// that requires the shorter idle timeout.
    /// </summary>
    /// <param name="path">The request path (e.g., "/api/payroll/runs").</param>
    /// <returns><c>true</c> if the path matches a privileged prefix; otherwise <c>false</c>.</returns>
    public static bool IsPrivilegedEndpoint(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        foreach (var prefix in PrivilegedPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether the session has expired based on the last activity timestamp
    /// and whether the target endpoint is privileged.
    /// </summary>
    /// <param name="lastActivity">The timestamp of the user's last recorded activity.</param>
    /// <param name="isPrivileged">Whether the target endpoint is classified as privileged.</param>
    /// <returns><c>true</c> if the idle time exceeds the applicable timeout; otherwise <c>false</c>.</returns>
    public static bool IsSessionExpired(DateTimeOffset lastActivity, bool isPrivileged)
    {
        var idleMinutes = (DateTimeOffset.UtcNow - lastActivity).TotalMinutes;
        var timeoutMinutes = isPrivileged ? PrivilegedIdleTimeoutMinutes : StandardIdleTimeoutMinutes;
        return idleMinutes > timeoutMinutes;
    }

    /// <summary>
    /// Determines whether the session has expired based on the last activity timestamp,
    /// whether the target endpoint is privileged, and a specific "now" timestamp (for testability).
    /// </summary>
    /// <param name="lastActivity">The timestamp of the user's last recorded activity.</param>
    /// <param name="isPrivileged">Whether the target endpoint is classified as privileged.</param>
    /// <param name="utcNow">The current UTC time to compare against.</param>
    /// <returns><c>true</c> if the idle time exceeds the applicable timeout; otherwise <c>false</c>.</returns>
    public static bool IsSessionExpired(DateTimeOffset lastActivity, bool isPrivileged, DateTimeOffset utcNow)
    {
        var idleMinutes = (utcNow - lastActivity).TotalMinutes;
        var timeoutMinutes = isPrivileged ? PrivilegedIdleTimeoutMinutes : StandardIdleTimeoutMinutes;
        return idleMinutes > timeoutMinutes;
    }
}
