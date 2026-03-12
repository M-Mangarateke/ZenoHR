// CTL-POPIA-008: Security incident lifecycle status — forward-only state machine.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Lifecycle status of a security incident — forward-only transitions enforced.
/// </summary>
public enum IncidentStatus
{
    Unknown = 0,
    Detected = 1,
    Investigating = 2,
    Contained = 3,
    Resolved = 4,
    FalsePositive = 5
}
