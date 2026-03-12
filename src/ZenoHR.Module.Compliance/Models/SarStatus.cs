// CTL-POPIA-009: POPIA §23 Subject Access Request lifecycle status.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Lifecycle status of a Subject Access Request — forward-only state machine.
/// POPIA §23 requires response within 30 calendar days.
/// </summary>
public enum SarStatus
{
    Unknown = 0,
    Submitted = 1,
    UnderReview = 2,
    DataGathering = 3,
    Completed = 4,
    Rejected = 5
}
