// REQ-HR-003, REQ-OPS-001, CTL-SARS-001: Domain event published when a run is finalized (immutable).
using ZenoHR.Domain.Events;
using ZenoHR.Domain.Common;
namespace ZenoHR.Module.Payroll.Events;
/// <summary>
/// Published when a <see cref="Aggregates.PayrollRun"/> is finalized and locked by the HR Manager or Director.
/// Handlers should trigger payslip PDF generation and EMP201 preparation.
/// The run is now <strong>immutable</strong>: no fields can be updated except the final
/// <c>Filed</c> status transition.
/// </summary>
public sealed record PayrollRunFinalizedEvent(
    string PayrollRunId,
    string Period,
    int EmployeeCount,
    MoneyZAR GrossTotal,
    MoneyZAR PayeTotal,
    MoneyZAR NetTotal,
    string Checksum) : DomainEvent;