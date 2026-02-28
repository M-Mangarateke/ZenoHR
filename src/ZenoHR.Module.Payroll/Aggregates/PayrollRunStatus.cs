// REQ-HR-003, PRD-16 Section 1: Payroll run lifecycle states matching Firestore schema (docs/schemas/firestore-collections.md §8.1).
namespace ZenoHR.Module.Payroll.Aggregates;

/// <summary>Lifecycle status of a <see cref="PayrollRun"/>.</summary>
/// <remarks>
/// State machine: <c>Draft → Calculated → Finalized → Filed</c>.
/// Only forward transitions are permitted. Finalized and Filed are terminal (immutable).
/// </remarks>
public enum PayrollRunStatus
{
    /// <summary>Guard value — should never appear on a persisted document.</summary>
    Unknown = 0,

    /// <summary>
    /// Run created by Director or HRManager. Employee contracts and approved timesheets
    /// are being loaded. Calculations have not yet run.
    /// </summary>
    Draft = 1,

    /// <summary>
    /// System has completed PAYE, UIF, SDL, ETI calculations for every employee using
    /// the active <c>StatutoryRuleSet</c>. Compliance flags have been written.
    /// Awaiting HR Manager or Director review and sign-off.
    /// </summary>
    Calculated = 2,

    /// <summary>
    /// HR Manager or Director has reviewed totals and clicked Finalize &amp; Lock.
    /// Record is now <strong>immutable</strong>. Only the <c>Filed</c> transition is permitted.
    /// </summary>
    Finalized = 3,

    /// <summary>
    /// EMP201 CSV has been downloaded. Run is fully closed for this pay period.
    /// No further state transitions permitted.
    /// </summary>
    Filed = 4
}
