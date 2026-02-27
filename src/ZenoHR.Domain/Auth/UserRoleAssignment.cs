// REQ-SEC-002: User role assignment domain record.
// REQ-SEC-003: Effective role and department scope derived from active assignments.
// PRD-15 Section 1.7: Users may hold multiple active assignments simultaneously.

using ZenoHR.Domain.Common;

namespace ZenoHR.Domain.Auth;

/// <summary>
/// Represents a single active or historical role assignment for a Firebase user.
/// Stored in the root Firestore <c>user_role_assignments</c> collection.
/// A user may have multiple active documents (dual roles, multi-department managers).
/// </summary>
/// <remarks>
/// PRD-15 Section 1.7: effective <see cref="SystemRole"/> = highest-privilege active assignment.
/// For Manager assignments, department scope = union of all active <see cref="DepartmentId"/> values.
/// </remarks>
public sealed record UserRoleAssignment
{
    /// <summary>Firestore document ID for this assignment.</summary>
    public required string AssignmentId { get; init; }

    /// <summary>Tenant that owns this assignment. Never null.</summary>
    public required string TenantId { get; init; }

    /// <summary>Firebase Auth UID of the assigned user.</summary>
    public required string FirebaseUid { get; init; }

    /// <summary>FK to <c>employees</c> collection — the user's employee record.</summary>
    public required string EmployeeId { get; init; }

    /// <summary>FK to <c>roles</c> collection or system role constant.</summary>
    public required string RoleId { get; init; }

    /// <summary>The base system role for this assignment.</summary>
    public required SystemRole SystemRole { get; init; }

    /// <summary>
    /// Department scope. Required for <see cref="SystemRole.Manager"/> assignments.
    /// Must be <see langword="null"/> for Director/HRManager (tenant-scoped roles).
    /// </summary>
    public string? DepartmentId { get; init; }

    /// <summary>
    /// <see langword="true"/> if this is the user's primary (highest-privilege) assignment.
    /// System-managed — exactly one active assignment per user must have this set.
    /// </summary>
    public bool IsPrimary { get; init; }

    /// <summary>
    /// <see langword="true"/> = active assignment; <see langword="false"/> = revoked.
    /// Assignments are never deleted — revocation sets this to <see langword="false"/>.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>First date on which this assignment is effective (inclusive).</summary>
    public DateOnly EffectiveFrom { get; init; }

    /// <summary>Last date on which this assignment is effective (inclusive). Null = indefinite.</summary>
    public DateOnly? EffectiveTo { get; init; }

    // ─── Business Logic ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> if this assignment is currently in force on
    /// <paramref name="today"/>.
    /// </summary>
    /// <remarks>
    /// PRD-15 Section 1.7: An "active" assignment satisfies:
    /// <c>is_active == true AND effective_from &lt;= today AND (effective_to IS NULL OR effective_to &gt;= today)</c>
    /// </remarks>
    public bool IsCurrentlyActive(DateOnly today) =>
        IsActive
        && EffectiveFrom <= today
        && (EffectiveTo is null || EffectiveTo.Value >= today);
}

/// <summary>
/// Pure-domain logic for resolving a user's effective role and department scope
/// from their set of <see cref="UserRoleAssignment"/> documents.
/// REQ-SEC-002, REQ-SEC-003.
/// </summary>
public static class UserRoleAssignmentResolver
{
    /// <summary>
    /// Returns the effective <see cref="SystemRole"/> for the given set of assignments
    /// on <paramref name="today"/>.
    /// </summary>
    /// <remarks>
    /// Effective role = highest-privilege active assignment.
    /// Higher privilege = lower enum integer value (SaasAdmin=1 &lt; Director=2 &lt; … &lt; Employee=5).
    /// Returns <see cref="SystemRole.Unknown"/> when no active assignments exist.
    /// PRD-15 Section 1.7 Resolution Rule 1.
    /// </remarks>
    public static SystemRole GetEffectiveSystemRole(
        IEnumerable<UserRoleAssignment> assignments, DateOnly today)
    {
        var activeRoles = assignments
            .Where(a => a.IsCurrentlyActive(today))
            .Select(a => a.SystemRole)
            .Where(r => r != SystemRole.Unknown)
            .ToList();

        if (activeRoles.Count == 0)
            return SystemRole.Unknown;

        // Lower int value = higher privilege — Min() gives the most privileged role.
        return (SystemRole)activeRoles.Min(r => (int)r);
    }

    /// <summary>
    /// Returns the union of all active <see cref="UserRoleAssignment.DepartmentId"/> values
    /// for <see cref="SystemRole.Manager"/> assignments on <paramref name="today"/>.
    /// </summary>
    /// <remarks>
    /// Used to build the <c>dept_ids</c> JWT claim for server-side team-scope filtering.
    /// PRD-15 Section 1.7 Resolution Rule 2.
    /// </remarks>
    public static IReadOnlyList<string> GetManagerDeptIds(
        IEnumerable<UserRoleAssignment> assignments, DateOnly today)
    {
        return assignments
            .Where(a => a.IsCurrentlyActive(today)
                     && a.SystemRole == SystemRole.Manager
                     && a.DepartmentId is not null)
            .Select(a => a.DepartmentId!)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Returns the employee ID from the primary active assignment on <paramref name="today"/>.
    /// Returns <see langword="null"/> if no active assignments exist.
    /// </summary>
    public static string? GetEmployeeId(
        IEnumerable<UserRoleAssignment> assignments, DateOnly today)
    {
        // Prefer primary assignment; fall back to first active
        var active = assignments
            .Where(a => a.IsCurrentlyActive(today))
            .ToList();

        return (active.FirstOrDefault(a => a.IsPrimary) ?? active.FirstOrDefault())
            ?.EmployeeId;
    }

    /// <summary>
    /// Returns the tenant ID from the primary active assignment on <paramref name="today"/>.
    /// Returns <see langword="null"/> if no active assignments exist.
    /// </summary>
    public static string? GetTenantId(
        IEnumerable<UserRoleAssignment> assignments, DateOnly today)
    {
        var active = assignments
            .Where(a => a.IsCurrentlyActive(today))
            .ToList();

        return (active.FirstOrDefault(a => a.IsPrimary) ?? active.FirstOrDefault())
            ?.TenantId;
    }
}
