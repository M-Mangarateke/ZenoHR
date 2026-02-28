// REQ-HR-001, CTL-BCEA-001, REQ-OPS-002: Domain event published when an employee is terminated.
using ZenoHR.Domain.Events;

namespace ZenoHR.Module.Employee.Events;

/// <summary>
/// Published when an employee's employment is terminated.
/// Handlers should stop leave accrual, exclude from future payroll runs,
/// and trigger the termination settlement workflow.
/// </summary>
public sealed record EmployeeTerminatedEvent(
    string EmployeeId,
    string TerminationReasonCode,
    DateOnly EffectiveDate) : DomainEvent;
