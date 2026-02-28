// REQ-HR-001, REQ-OPS-002: Domain event published when an employee is suspended.
using ZenoHR.Domain.Events;

namespace ZenoHR.Module.Employee.Events;

/// <summary>
/// Published when an employee's employment is suspended (e.g. pending disciplinary outcome).
/// Handlers should exclude the employee from the next payroll run until reactivated.
/// </summary>
public sealed record EmployeeSuspendedEvent(
    string EmployeeId,
    string Reason) : DomainEvent;
