// VUL-008: Manager department scoping enforcement at API layer.
// REQ-SEC-002: PRD-15 §1.7 — Manager queries scoped to their department(s).
// Multi-dept Managers see the union of all managed departments.

using System.Security.Claims;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Api.Auth;

/// <summary>
/// Filters collections by department scope based on the authenticated user's role and department claims.
/// <para>
/// Director and HRManager see all items (no filtering). Manager sees only items belonging to
/// their assigned department(s). Employee gets an empty result (they use self-access endpoints).
/// SaasAdmin gets an empty result (no tenant data access).
/// </para>
/// </summary>
/// <remarks>
/// VUL-008: This enforcement happens at the API layer — not just Firestore rules.
/// PRD-15 §1.7: Multi-dept Manager scope = union of all managed departments (combined view).
/// </remarks>
public sealed class DepartmentScopeFilter
{
    /// <summary>
    /// Filters a collection of items based on the authenticated user's department scope.
    /// </summary>
    /// <typeparam name="T">The type of items to filter.</typeparam>
    /// <param name="items">The items to filter by department scope.</param>
    /// <param name="departmentSelector">A function that extracts the department ID from each item.</param>
    /// <param name="user">The authenticated user's <see cref="ClaimsPrincipal"/>.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the filtered items, or a failure if the user
    /// has no valid role claim.
    /// </returns>
    // VUL-008, REQ-SEC-002
    public Result<IReadOnlyList<T>> FilterByDepartmentScope<T>(
        IEnumerable<T> items,
        Func<T, string> departmentSelector,
        ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(departmentSelector);
        ArgumentNullException.ThrowIfNull(user);

        var roleClaim = user.FindFirstValue(ClaimTypes.Role);

        if (string.IsNullOrWhiteSpace(roleClaim)
            || !Enum.TryParse<SystemRole>(roleClaim, ignoreCase: true, out var role)
            || role == SystemRole.Unknown)
        {
            return Result<IReadOnlyList<T>>.Failure(
                ZenoHrErrorCode.Unauthorized,
                "No valid system role claim found on the authenticated user.");
        }

        // Director and HRManager see all items — no department filtering. // REQ-SEC-002
        if (role is SystemRole.Director or SystemRole.HRManager)
        {
            return Result<IReadOnlyList<T>>.Success(items.ToList().AsReadOnly());
        }

        // Employee uses self-access endpoints — department-scoped queries return empty. // REQ-SEC-002
        // SaasAdmin has no tenant data access — also returns empty.
        if (role is SystemRole.Employee or SystemRole.SaasAdmin)
        {
            return Result<IReadOnlyList<T>>.Success(Array.Empty<T>());
        }

        // Manager — filter to union of assigned department(s). // VUL-008, PRD-15 §1.7
        var deptIds = user.FindAll(ZenoHrClaimNames.DeptId)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (deptIds.Count == 0)
        {
            // Manager with no department claims — return empty (no scope to grant).
            return Result<IReadOnlyList<T>>.Success(Array.Empty<T>());
        }

        var filtered = items
            .Where(item => deptIds.Contains(departmentSelector(item)))
            .ToList()
            .AsReadOnly();

        return Result<IReadOnlyList<T>>.Success(filtered);
    }
}
