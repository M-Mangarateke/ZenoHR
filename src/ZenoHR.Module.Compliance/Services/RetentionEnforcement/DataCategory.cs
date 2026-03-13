// CTL-POPIA-015: Data categories for retention period determination.
// POPIA §14 — different data types have different retention requirements.

namespace ZenoHR.Module.Compliance.Services.RetentionEnforcement;

/// <summary>
/// Categories of data subject to different retention periods under POPIA §14 and BCEA §31.
/// </summary>
public enum DataCategory
{
    /// <summary>Forward-compatible default.</summary>
    Unknown = 0,

    /// <summary>Payroll records — BCEA §31 requires 3-year minimum retention.</summary>
    Payroll = 1,

    /// <summary>General employee personal information — POPIA default 5-year retention.</summary>
    General = 2,

    /// <summary>Audit trail records — 7-year retention for tamper-evidence chain.</summary>
    AuditTrail = 3,

    /// <summary>Leave records — follows general POPIA retention.</summary>
    Leave = 4,

    /// <summary>Time and attendance records — follows general POPIA retention.</summary>
    TimeAttendance = 5,

    /// <summary>Compliance submission records — follows audit trail retention.</summary>
    ComplianceSubmission = 6
}
