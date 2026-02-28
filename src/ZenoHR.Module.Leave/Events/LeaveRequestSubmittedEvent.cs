// REQ-HR-002, REQ-OPS-002: Domain event raised when an employee submits a leave request.
using ZenoHR.Domain.Events;
using ZenoHR.Module.Leave.Aggregates;

namespace ZenoHR.Module.Leave.Events;

/// <summary>
/// Published when an employee submits a new leave request.
/// Handlers route the request to the approving manager.
/// </summary>
public sealed record LeaveRequestSubmittedEvent(
    string LeaveRequestId,
    string EmployeeId,
    LeaveType LeaveType,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalHours) : DomainEvent;
