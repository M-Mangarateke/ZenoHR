// REQ-COMP-003, CTL-POPIA-012: Data lifecycle status for retention and legal-hold management.
namespace ZenoHR.Module.Payroll.Aggregates;

/// <summary>Data lifecycle status for a <see cref="PayrollRun"/> (POPIA retention compliance).</summary>
public enum PayrollRunDataStatus
{
    Unknown = 0,

    /// <summary>Active record — visible in reports and queries.</summary>
    Active = 1,

    /// <summary>Retention window expired — moved to cold storage. Not visible in UI.</summary>
    Archived = 2,

    /// <summary>Legal hold applied — archival suspended regardless of retention schedule.</summary>
    LegalHold = 3
}
