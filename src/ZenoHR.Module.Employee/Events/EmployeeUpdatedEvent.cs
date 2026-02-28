// REQ-HR-001, REQ-OPS-002: Domain event published when an employee profile field is changed.
using ZenoHR.Domain.Events;

namespace ZenoHR.Module.Employee.Events;

/// <summary>
/// Published when mutable fields on an employee record are updated by HR Manager or Director.
/// Handlers write audit trail entries for all changed fields.
/// </summary>
public sealed record EmployeeUpdatedEvent(
    string EmployeeId,
    IReadOnlyList<string> ChangedFields) : DomainEvent;
