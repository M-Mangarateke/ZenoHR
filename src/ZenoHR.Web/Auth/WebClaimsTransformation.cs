// REQ-SEC-002: Maps Firebase JWT claims to ASP.NET Core role claims for [Authorize(Roles)] enforcement.
// REQ-SEC-003: Effective role and department scope derived from active user_role_assignments.
// TC-SEC-001: Every Blazor session with a valid cookie gets exactly one ClaimTypes.Role claim.
// Adapted for ZenoHR.Web (Blazor Server cookie auth). Canonical version: ZenoHR.Api.Auth.ZenoHrClaimsTransformation.

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using ZenoHR.Domain.Auth;
using ZenoHR.Domain.Common;
using ZenoHR.Infrastructure.Auth;

namespace ZenoHR.Web.Auth;

/// <summary>
/// Blazor Server ASP.NET Core claims transformation that enriches the <see cref="ClaimsPrincipal"/>
/// with ZenoHR role and tenant claims after Firebase cookie session is established.
/// </summary>
public sealed partial class WebClaimsTransformation : IClaimsTransformation
{
    internal const int CacheTtlMinutes = 5;

    private readonly UserRoleAssignmentRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WebClaimsTransformation> _logger;

    public WebClaimsTransformation(
        UserRoleAssignmentRepository repository,
        IMemoryCache cache,
        ILogger<WebClaimsTransformation> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return principal;

        if (principal.HasClaim(c => c.Type == ClaimTypes.Role))
            return principal;

        var uid = principal.FindFirstValue("user_id")
                  ?? principal.FindFirstValue("sub")
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(uid))
        {
            LogNoUidClaim(_logger);
            return principal;
        }

        // Fast path: Firebase custom claims already present in the JWT/cookie
        var systemRoleStr = principal.FindFirstValue(ZenoHrClaimNames.SystemRoleJwt);
        var tenantId = principal.FindFirstValue(ZenoHrClaimNames.TenantId);
        var employeeId = principal.FindFirstValue(ZenoHrClaimNames.EmployeeId);
        var deptIdsRaw = principal.FindFirstValue(ZenoHrClaimNames.DeptIds);

        if (systemRoleStr is not null)
        {
            return EnrichPrincipal(
                principal, uid, systemRoleStr, tenantId, employeeId,
                deptIdsRaw?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? []);
        }

        // Slow path: query Firestore
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
                return principal;
            }
        }

        if (cached.SystemRole is null)
            return principal;

        return EnrichPrincipal(
            principal, uid, cached.SystemRole, cached.TenantId, cached.EmployeeId, cached.DeptIds);
    }

    private static ClaimsPrincipal EnrichPrincipal(
        ClaimsPrincipal original,
        string uid,
        string systemRole,
        string? tenantId,
        string? employeeId,
        IEnumerable<string> deptIds)
    {
        var identity = new ClaimsIdentity(original.Identity);
        identity.AddClaim(new Claim(ClaimTypes.Role, systemRole));
        if (tenantId is not null)
            identity.AddClaim(new Claim(ZenoHrClaimNames.TenantId, tenantId));
        if (employeeId is not null)
            identity.AddClaim(new Claim(ZenoHrClaimNames.EmployeeId, employeeId));
        foreach (var deptId in deptIds)
            identity.AddClaim(new Claim(ZenoHrClaimNames.DeptId, deptId));
        return new ClaimsPrincipal(identity);
    }

    private sealed record RbacCacheEntry(
        string? SystemRole, string? TenantId, string? EmployeeId, IReadOnlyList<string> DeptIds);

    [LoggerMessage(EventId = 9000, Level = LogLevel.Warning,
        Message = "ZenoHR Web claims transformation: authenticated principal has no UID claim.")]
    private static partial void LogNoUidClaim(ILogger logger);

    [LoggerMessage(EventId = 9001, Level = LogLevel.Debug,
        Message = "ZenoHR Web RBAC: no JWT custom claims for UID {Uid}; loading from Firestore.")]
    private static partial void LogLoadingFromFirestore(ILogger logger, string uid);

    [LoggerMessage(EventId = 9002, Level = LogLevel.Debug,
        Message = "ZenoHR Web RBAC: resolved role={Role} tenant={Tenant} for UID {Uid}.")]
    private static partial void LogResolvedRole(ILogger logger, string? role, string? tenant, string uid);

    [LoggerMessage(EventId = 9003, Level = LogLevel.Error,
        Message = "ZenoHR Web RBAC: failed to load role assignments from Firestore for UID {Uid}.")]
    private static partial void LogFirestoreLoadFailed(ILogger logger, string uid, Exception ex);
}
