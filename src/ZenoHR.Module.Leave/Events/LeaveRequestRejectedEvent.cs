// REQ-HR-002, REQ-OPS-002: Domain event raised when a manager rejects a leave request.
using ZenoHR.Domain.Events;
using ZenoHR.Module.Leave.Aggregates;

namespace ZenoHR.Module.Leave.Events;

/// <summary>
/// Published when a manager rejects a leave request.
/// Handlers send rejection notification to the employee.
/// </summary>
public sealed record LeaveRequestRejectedEvent(
    string LeaveRequestId,
    string EmployeeId,
    string ApproverId,
    LeaveType LeaveType,
    string RejectionReason) : DomainEvent;
