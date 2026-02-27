// REQ-OPS-003: AuditAction — defines the set of auditable actions in ZenoHR.
// Every AuditEvent carries exactly one action that describes the type of change.
// Maps to POPIA Chapter 4 (processing conditions) and BCEA record-keeping requirements.

namespace ZenoHR.Module.Audit.Domain;

/// <summary>
/// Categorises the type of action recorded in an <see cref="AuditEvent"/>.
/// </summary>
public enum AuditAction
{
    /// <summary>Unknown action — forward-compatibility sentinel. Must never appear in production data.</summary>
    Unknown = 0,

    /// <summary>A new resource was created (e.g., employee record, payroll run).</summary>
    Create = 1,

    /// <summary>An existing resource was read/accessed (PII access logging for POPIA CTL-POPIA-006).</summary>
    Read = 2,

    /// <summary>An existing resource was modified.</summary>
    Update = 3,

    /// <summary>A resource was soft-deleted or archived (hard deletion is forbidden in ZenoHR).</summary>
    Delete = 4,

    /// <summary>A payroll run, leave request, or compliance submission was finalised (write-locked).</summary>
    Finalize = 5,

    /// <summary>A leave request, timesheet, or approval-workflow document was approved.</summary>
    Approve = 6,

    /// <summary>A leave request, timesheet, or approval-workflow document was rejected.</summary>
    Reject = 7,

    /// <summary>A compliance document or EMP201 package was submitted to SARS or another authority.</summary>
    Submit = 8,

    /// <summary>A user signed in to ZenoHR (Firebase Auth login event).</summary>
    Login = 9,

    /// <summary>A user signed out of ZenoHR.</summary>
    Logout = 10,

    /// <summary>Data was exported to a file (CSV, PDF, ZIP) — triggers POPIA data export log entry.</summary>
    Export = 11,

    /// <summary>A payroll adjustment was applied to a finalised run (correction workflow).</summary>
    Adjust = 12,

    /// <summary>A record's status was changed without a full update (e.g., archived, legal hold applied).</summary>
    StatusChange = 13,
}
