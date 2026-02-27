// REQ-SEC-002: Maps Firebase JWT claims to ASP.NET Core role claims for [Authorize(Roles)] enforcement.
// REQ-SEC-003: Effective role and department scope derived from active user_role_assignments.
// TC-SEC-001: Every request with a valid Firebase JWT gets exactly one ClaimTypes.Role claim.

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ZenoHR.Domain.Auth;
using ZenoHR.Domain.Common;
using ZenoHR.Infrastructure.Auth;

namespace ZenoHR.Api.Auth;

/// <summary>
/// ASP.NET Core claims transformation that enriches the <see cref="ClaimsPrincipal"/>
/// with ZenoHR role and tenant claims after Firebase JWT validation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Fast path</strong>: Firebase custom claims (<c>system_role</c>, <c>tenant_id</c>,
/// <c>employee_id</c>, <c>dept_ids</c>) are already embedded in the JWT by Firebase Admin SDK.
/// The transformation simply re-maps these to ASP.NET Core <see cref="ClaimTypes.Role"/>.
/// </para>
/// <para>
/// <strong>Slow path</strong>: When Firebase custom claims are absent (development environment
/// or first request after onboarding), the transformation queries Firestore
/// <c>user_role_assignments</c> and caches the result for <see cref="CacheTtlMinutes"/> minutes.
/// </para>
/// </remarks>
public sealed partial class ZenoHrClaimsTransformation : IClaimsTransformation
{
    /// <summary>How long resolved RBAC data is cached per user. PRD-15: claims refresh on role change.</summary>
    internal const int CacheTtlMinutes = 5;

    private readonly UserRoleAssignmentRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ZenoHrClaimsTransformation> _logger;

    public ZenoHrClaimsTransformation(
        UserRoleAssignmentRepository repository,
        IMemoryCache cache,
        ILogger<ZenoHrClaimsTransformation> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return principal;

        // Avoid double-transformation within the same request
        if (principal.HasClaim(c => c.Type == ClaimTypes.Role))
            return principal;

        // Firebase JWT stores the user ID in the "sub" claim (or ClaimTypes.NameIdentifier
        // depending on the JwtBearerHandler claim-mapping configuration).
        var uid = principal.FindFirstValue("user_id")       // Firebase JWT 'user_id' claim
                  ?? principal.FindFirstValue("sub")        // Standard JWT subject
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(uid))
        {
            LogNoUidClaim(_logger);
            return principal;
        }

        // ── Fast path: Firebase custom claims already in the JWT ─────────────
        var systemRoleStr = principal.FindFirstValue(ZenoHrClaimNames.SystemRoleJwt);
        var tenantId = principal.FindFirstValue(ZenoHrClaimNames.TenantId);
        var employeeId = principal.FindFirstValue(ZenoHrClaimNames.EmployeeId);
        var deptIdsRaw = principal.FindFirstValue(ZenoHrClaimNames.DeptIds);

        if (systemRoleStr is not null)
        {
            // Firebase custom claims are present — just remap to ASP.NET Core types
            return EnrichPrincipal(
                principal, uid, systemRoleStr, tenantId, employeeId,
                deptIdsRaw?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? []);
        }

        // ── Slow path: query Firestore for role assignments ──────────────────
        var cacheKey = $"rbac:{uid}:{tenantId ?? "any"}";

        if (!_cache.TryGetValue(cacheKey, out RbacCacheEntry? cached) || cached is null)
        {
            LogLoadingFromFirestore(_logger, uid);

            try
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var assignments = await _repository.GetActiveAssignmentsAsync(uid, tenantId);

                var resolvedRole = UserRoleAssignmentResolver.GetEffectiveSystemRole(assignments, today);
                var resolvedDeptIds = UserRoleAssignmentResolver.GetManagerDeptIds(assignments, today);
                var resolvedEmployeeId = UserRoleAssignmentResolver.GetEmployeeId(assignments, today);
                var resolvedTenantId = UserRoleAssignmentResolver.GetTenantId(assignments, today) ?? tenantId;

                cached = new RbacCacheEntry(
                    SystemRole: resolvedRole == SystemRole.Unknown ? null : resolvedRole.ToString(),
                    TenantId: resolvedTenantId,
                    EmployeeId: resolvedEmployeeId,
                    DeptIds: [.. resolvedDeptIds]);

                _cache.Set(cacheKey, cached, TimeSpan.FromMinutes(CacheTtlMinutes));

                LogResolvedRole(_logger, cached.SystemRole, cached.TenantId, uid);
            }
            catch (Exception ex)
            {
                LogFirestoreLoadFailed(_logger, uid, ex);
                // Fall through — return principal without role claims (will be denied by [Authorize])
                return principal;
            }
        }

        if (cached.SystemRole is null)
            return principal; // No active assignments — deny at [Authorize]

        return EnrichPrincipal(
            principal, uid, cached.SystemRole, cached.TenantId, cached.EmployeeId, cached.DeptIds);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static ClaimsPrincipal EnrichPrincipal(
        ClaimsPrincipal original,
        string uid,
        string systemRole,
        string? tenantId,
        string? employeeId,
        IEnumerable<string> deptIds)
    {
        var identity = new ClaimsIdentity(original.Identity);

        // ClaimTypes.Role is what [Authorize(Roles = "Director,HRManager")] checks.
        identity.AddClaim(new Claim(ClaimTypes.Role, systemRole));

        if (tenantId is not null)
            identity.AddClaim(new Claim(ZenoHrClaimNames.TenantId, tenantId));

        if (employeeId is not null)
            identity.AddClaim(new Claim(ZenoHrClaimNames.EmployeeId, employeeId));

        foreach (var deptId in deptIds)
            identity.AddClaim(new Claim(ZenoHrClaimNames.DeptId, deptId));

        return new ClaimsPrincipal(identity);
    }

    /// <summary>Cached RBAC resolution result (per uid, per tenant).</summary>
    private sealed record RbacCacheEntry(
        string? SystemRole,
        string? TenantId,
        string? EmployeeId,
        IReadOnlyList<string> DeptIds);

    // ─── Source-generated LoggerMessage delegates (CA1848) ───────────────────

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "ZenoHR claims transformation: authenticated principal has no UID claim.")]
    private static partial void LogNoUidClaim(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "ZenoHR RBAC: no JWT custom claims for UID {Uid}; loading from Firestore.")]
    private static partial void LogLoadingFromFirestore(ILogger logger, string uid);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "ZenoHR RBAC: resolved role={Role} tenant={Tenant} for UID {Uid}.")]
    private static partial void LogResolvedRole(ILogger logger, string? role, string? tenant, string uid);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "ZenoHR RBAC: failed to load role assignments from Firestore for UID {Uid}.")]
    private static partial void LogFirestoreLoadFailed(ILogger logger, string uid, Exception ex);
}
