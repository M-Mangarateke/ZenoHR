// REQ-HR-003, REQ-OPS-001: Domain event published when a payroll run is created.
using ZenoHR.Domain.Events;
using ZenoHR.Module.Payroll.Aggregates;
namespace ZenoHR.Module.Payroll.Events;
/// <summary>
/// Published when a new <see cref="PayrollRun"/> is created in Draft status.
/// Handlers should trigger the calculation pipeline.
/// </summary>
public sealed record PayrollRunCreatedEvent(
    string PayrollRunId,
    string Period,
    string RunType) : DomainEvent;