// VUL-014: RoleChangeAction — categorises role assignment audit events.
// REQ-SEC-002: Role changes must be tracked for privilege escalation detection.

namespace ZenoHR.Module.Audit.Models;

/// <summary>
/// Identifies the type of role change recorded in a <see cref="RoleChangeAuditRecord"/>.
/// </summary>
public enum RoleChangeAction
{
    /// <summary>Unknown action — forward-compatibility sentinel.</summary>
    Unknown = 0,

    /// <summary>A role was assigned to an employee.</summary>
    Assigned = 1,

    /// <summary>A role was revoked from an employee.</summary>
    Revoked = 2,

    /// <summary>A custom role's permissions were modified.</summary>
    Modified = 3,
}
