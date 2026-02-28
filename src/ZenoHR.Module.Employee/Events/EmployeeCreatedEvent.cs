// REQ-HR-001, REQ-OPS-002: Domain event published when a new employee record is created.
using ZenoHR.Domain.Events;

namespace ZenoHR.Module.Employee.Events;

/// <summary>
/// Published when a new employee profile is created.
/// Handlers can trigger onboarding workflows, leave balance initialisation, and audit trail.
/// </summary>
public sealed record EmployeeCreatedEvent(
    string EmployeeId,
    string LegalName,
    string FirebaseUid,
    string DepartmentId,
    string SystemRole) : DomainEvent;
