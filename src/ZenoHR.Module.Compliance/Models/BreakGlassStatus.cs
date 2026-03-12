// REQ-SEC-008: Break-glass emergency access lifecycle status.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Lifecycle status of a break-glass emergency access request — forward-only state machine.
/// Flow: Requested → Approved → Active → Expired/Revoked → PostReviewPending → Closed.
/// </summary>
public enum BreakGlassStatus
{
    Unknown = 0,
    Requested = 1,
    Approved = 2,
    Active = 3,
    Expired = 4,
    Revoked = 5,
    PostReviewPending = 6,
    Closed = 7,
}
