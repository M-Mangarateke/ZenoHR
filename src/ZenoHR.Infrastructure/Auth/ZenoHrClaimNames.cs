// REQ-SEC-002: Claim name constants shared across the ZenoHR RBAC middleware.
// Shared by ZenoHR.Api (JWT Bearer) and ZenoHR.Web (Cookie auth + exchange endpoint).

namespace ZenoHR.Infrastructure.Auth;

/// <summary>
/// Constants for custom claim names used in the ZenoHR RBAC system.
/// </summary>
/// <remarks>
/// Firebase custom claims (set via Firebase Admin SDK during onboarding/role changes) appear
/// in the Firebase JWT and are read directly from the <see cref="System.Security.Claims.ClaimsPrincipal"/>.
/// The <see cref="ZenoHrClaimsTransformation"/> maps these to the correct ASP.NET Core claim types.
/// </remarks>
public static class ZenoHrClaimNames
{
    /// <summary>
    /// Firebase JWT claim name for the highest-privilege system role string.
    /// Set by Firebase Admin SDK as a custom claim during onboarding and role changes.
    /// Value: <c>"Director"</c>, <c>"HRManager"</c>, <c>"Manager"</c>, <c>"Employee"</c>, or <c>"SaasAdmin"</c>.
    /// </summary>
    public const string SystemRoleJwt = "system_role";

    /// <summary>
    /// Tenant identifier. Set as a Firebase custom claim during tenant onboarding.
    /// Present on all tenant user JWTs. Absent for SaasAdmin.
    /// </summary>
    public const string TenantId = "tenant_id";

    /// <summary>
    /// The employee Firestore document ID for the authenticated user.
    /// Set as a Firebase custom claim. Absent for SaasAdmin (no employee record).
    /// </summary>
    public const string EmployeeId = "employee_id";

    /// <summary>
    /// Comma-separated list of managed department IDs for Manager-role users.
    /// Example: <c>"dept_finance,dept_operations"</c>.
    /// Empty string or absent for non-Manager roles.
    /// </summary>
    public const string DeptIds = "dept_ids";

    /// <summary>
    /// Single department ID claim added per managed department for multi-dept Managers.
    /// Multiple claims with this type may be present on the <see cref="System.Security.Claims.ClaimsPrincipal"/>.
    /// </summary>
    public const string DeptId = "dept_id";
}
