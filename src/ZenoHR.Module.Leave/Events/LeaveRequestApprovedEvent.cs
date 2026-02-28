// REQ-HR-002, CTL-BCEA-003, REQ-OPS-002: Domain event raised when leave is approved.
using ZenoHR.Domain.Events;
using ZenoHR.Module.Leave.Aggregates;

namespace ZenoHR.Module.Leave.Events;

/// <summary>
/// Published when a manager approves a leave request.
/// Handlers should consume the leave balance and send notification to the employee.
/// </summary>
public sealed record LeaveRequestApprovedEvent(
    string LeaveRequestId,
    string EmployeeId,
    string ApproverId,
    LeaveType LeaveType,
    decimal TotalHours) : DomainEvent;
