// VUL-014: RoleChangeAuditRecord — immutable record of a role assignment change.
// REQ-SEC-002: Captures who changed what role, for whom, and when.

namespace ZenoHR.Module.Audit.Models;

/// <summary>
/// Immutable record representing a single role assignment change event.
/// Each record is uniquely identified and carries structured metadata as JSON.
/// </summary>
public sealed record RoleChangeAuditRecord
{
    /// <summary>Unique identifier for this audit record (UUIDv7).</summary>
    public required string RecordId { get; init; }

    /// <summary>Tenant that owns this record.</summary>
    public required string TenantId { get; init; }

    /// <summary>The type of role change that occurred.</summary>
    public required RoleChangeAction Action { get; init; }

    /// <summary>The employee whose role was changed.</summary>
    public required string EmployeeId { get; init; }

    /// <summary>The name of the role that was assigned, revoked, or modified.</summary>
    public required string RoleName { get; init; }

    /// <summary>The department scope of the role assignment (if applicable).</summary>
    public required string DepartmentId { get; init; }

    /// <summary>The actor who performed the role change (Firebase UID).</summary>
    public required string PerformedBy { get; init; }

    /// <summary>UTC timestamp when the role change occurred.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>JSON string with additional details (reason, permission changes, etc.).</summary>
    public required string Metadata { get; init; }
}
