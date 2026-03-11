// CTL-POPIA-010, CTL-POPIA-011: POPIA breach lifecycle status.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Lifecycle status of a POPIA breach — forward-only state machine.
/// </summary>
public enum BreachStatus
{
    Unknown = 0,
    Detected = 1,
    Investigating = 2,
    Contained = 3,
    NotificationPending = 4,
    RegulatorNotified = 5,
    SubjectsNotified = 6,
    Remediated = 7,
    Closed = 8
}
