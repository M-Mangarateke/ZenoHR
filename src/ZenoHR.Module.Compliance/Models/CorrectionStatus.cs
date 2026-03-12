// CTL-POPIA-010: POPIA §24 — correction request lifecycle status.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Lifecycle status of a personal information correction request — forward-only state machine.
/// POPIA Act §24 grants data subjects the right to request correction or deletion.
/// </summary>
public enum CorrectionStatus
{
    Unknown = 0,
    Submitted = 1,
    UnderReview = 2,
    Approved = 3,
    Applied = 4,
    Rejected = 5
}
