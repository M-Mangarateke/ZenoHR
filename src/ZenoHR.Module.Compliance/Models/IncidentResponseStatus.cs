// CTL-POPIA-012, REQ-SEC-009: Incident response lifecycle status — forward-only state machine.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Lifecycle status of an incident response — forward-only transitions enforced.
/// Detected→Classified→Contained→Investigating→Recovered→PostReview→Closed.
/// </summary>
public enum IncidentResponseStatus
{
    Unknown = 0,
    Detected = 1,
    Classified = 2,
    Contained = 3,
    Investigating = 4,
    Recovered = 5,
    PostReview = 6,
    Closed = 7
}
