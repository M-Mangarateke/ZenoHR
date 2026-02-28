// REQ-HR-001, REQ-OPS-002: Domain event published when a suspended employee is reactivated.
using ZenoHR.Domain.Events;

namespace ZenoHR.Module.Employee.Events;

/// <summary>
/// Published when a suspended employee's status is returned to Active.
/// Handlers should re-include the employee in the next payroll run.
/// </summary>
public sealed record EmployeeReactivatedEvent(
    string EmployeeId) : DomainEvent;
