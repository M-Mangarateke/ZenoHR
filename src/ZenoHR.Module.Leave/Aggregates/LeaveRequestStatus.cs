// REQ-HR-002: Leave request lifecycle state machine.

namespace ZenoHR.Module.Leave.Aggregates;

/// <summary>
/// Lifecycle state of a leave request.
/// State machine: Submitted → ManagerReview → Approved | Rejected | Cancelled
/// </summary>
public enum LeaveRequestStatus
{
    Unknown = 0,

    /// <summary>Employee has submitted the request. Awaiting manager action.</summary>
    Submitted = 1,

    /// <summary>Manager is reviewing. Intermediate state for complex cases.</summary>
    ManagerReview = 2,

    /// <summary>Manager has approved. Leave balance consumed atomically.</summary>
    Approved = 3,

    /// <summary>Manager has rejected. Leave balance NOT consumed.</summary>
    Rejected = 4,

    /// <summary>Employee cancelled the request before approval. Leave balance NOT consumed.</summary>
    Cancelled = 5,
}
