// REQ-HR-003, REQ-OPS-001: Domain event published when calculations complete (Draft -> Calculated).
using ZenoHR.Domain.Events;
using ZenoHR.Domain.Common;
namespace ZenoHR.Module.Payroll.Events;
/// <summary>
/// Published when PAYE/UIF/SDL/ETI calculations complete for all employees in a run.
/// The run transitions from <c>Draft</c> to <c>Calculated</c>.
/// Handlers can trigger compliance scoring and notification to HR Manager.
/// </summary>
public sealed record PayrollRunCalculatedEvent(
    string PayrollRunId,
    string Period,
    int EmployeeCount,
    MoneyZAR GrossTotal,
    MoneyZAR PayeTotal,
    MoneyZAR UifTotal,
    MoneyZAR SdlTotal,
    MoneyZAR NetTotal,
    IReadOnlyList<string> ComplianceFlags) : DomainEvent;