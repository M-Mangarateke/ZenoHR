// CTL-POPIA-007: Status lifecycle for monthly access review records.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Lifecycle status of a monthly access review record.
/// Transitions: Pending → InReview → Approved | Rejected.
/// </summary>
public enum AccessReviewStatus
{
    Unknown = 0,
    Pending = 1,
    InReview = 2,
    Approved = 3,
    Rejected = 4,
}
