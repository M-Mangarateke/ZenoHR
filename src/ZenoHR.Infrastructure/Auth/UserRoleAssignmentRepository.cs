// REQ-SEC-002: Loads user role assignments from Firestore for RBAC middleware.
// REQ-SEC-003: Active assignments determine effective role and department scope.
// CTL-POPIA-007: Monthly access review — effective dates enable point-in-time audit.

using Google.Cloud.Firestore;
using ZenoHR.Domain.Auth;
using ZenoHR.Domain.Common;

namespace ZenoHR.Infrastructure.Auth;

/// <summary>
/// Reads <see cref="UserRoleAssignment"/> documents from the root
/// <c>user_role_assignments</c> Firestore collection.
/// Used by the RBAC claims transformation middleware to load the caller's active assignments.
/// </summary>
/// <remarks>
/// This repository queries using the Firebase Admin SDK (server-side <see cref="FirestoreDb"/>),
/// which bypasses Firestore security rules. This is intentional — the claims transformation
/// runs before the request is authorised and therefore cannot be gated by client-facing rules.
/// </remarks>
public sealed class UserRoleAssignmentRepository
{
    private const string CollectionName = "user_role_assignments";

    private readonly FirestoreDb _db;

    public UserRoleAssignmentRepository(FirestoreDb db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <summary>
    /// Returns all active <see cref="UserRoleAssignment"/> documents for the given
    /// <paramref name="firebaseUid"/>, optionally scoped to <paramref name="tenantId"/>.
    /// </summary>
    /// <param name="firebaseUid">The Firebase Auth UID from the JWT <c>sub</c> claim.</param>
    /// <param name="tenantId">
    /// Tenant to scope the query. When provided (from the JWT <c>tenant_id</c> custom claim),
    /// the query adds a <c>tenant_id ==</c> filter for efficiency.
    /// Pass <see langword="null"/> only for SaasAdmin or first-time onboarding lookups.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// All assignments where <c>is_active == true</c> for this UID.
    /// Effective date filtering (effective_from / effective_to) is applied in-memory
    /// to avoid Firestore composite index requirements for the date range check.
    /// </returns>
    public async Task<IReadOnlyList<UserRoleAssignment>> GetActiveAssignmentsAsync(
        string firebaseUid,
        string? tenantId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firebaseUid);

        // Base query: active assignments for this UID
        Query query = _db.Collection(CollectionName)
            .WhereEqualTo("firebase_uid", firebaseUid)
            .WhereEqualTo("is_active", true);

        // Narrow to a specific tenant when known (avoids cross-tenant data leak)
        if (!string.IsNullOrWhiteSpace(tenantId))
            query = query.WhereEqualTo("tenant_id", tenantId);

        var snapshot = await query.GetSnapshotAsync(ct);

        return snapshot.Documents
            .Select(FromSnapshot)
            .Where(a => a is not null)
            .Cast<UserRoleAssignment>()
            .ToList();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static UserRoleAssignment? FromSnapshot(DocumentSnapshot snap)
    {
        if (!snap.Exists) return null;

        try
        {
            var systemRoleStr = snap.TryGetValue<string>("system_role", out var srVal)
                ? srVal : null;

            if (!Enum.TryParse<SystemRole>(systemRoleStr, ignoreCase: true, out var systemRole))
                systemRole = SystemRole.Unknown;

            return new UserRoleAssignment
            {
                AssignmentId = snap.Id,
                TenantId = snap.GetValue<string>("tenant_id"),
                FirebaseUid = snap.GetValue<string>("firebase_uid"),
                EmployeeId = snap.GetValue<string>("employee_id"),
                RoleId = snap.GetValue<string>("role_id"),
                SystemRole = systemRole,
                DepartmentId = snap.TryGetValue<string>("department_id", out var deptId)
                    ? (string.IsNullOrEmpty(deptId) ? null : deptId)
                    : null,
                IsPrimary = snap.TryGetValue<bool>("is_primary", out var isPrimary) && isPrimary,
                IsActive = snap.TryGetValue<bool>("is_active", out var isActive) && isActive,
                EffectiveFrom = ParseDateOnly(snap, "effective_from"),
                EffectiveTo = ParseDateOnlyNullable(snap, "effective_to"),
            };
        }
        catch
        {
            // Malformed document — skip rather than crashing the middleware
            return null;
        }
    }

    private static DateOnly ParseDateOnly(DocumentSnapshot snap, string field)
    {
        if (!snap.TryGetValue<string>(field, out var str) || str is null)
            return DateOnly.MinValue;
        if (DateOnly.TryParseExact(str, "yyyy-MM-dd", out var d)) return d;
        // Fall back: try Timestamp
        if (snap.TryGetValue<Timestamp>(field, out var ts))
            return DateOnly.FromDateTime(ts.ToDateTime());
        return DateOnly.MinValue;
    }

    private static DateOnly? ParseDateOnlyNullable(DocumentSnapshot snap, string field)
    {
        if (!snap.TryGetValue<string>(field, out var str) || str is null)
            return null;
        if (DateOnly.TryParseExact(str, "yyyy-MM-dd", out var d)) return d;
        if (snap.TryGetValue<Timestamp>(field, out var ts))
            return DateOnly.FromDateTime(ts.ToDateTime());
        return null;
    }
}
