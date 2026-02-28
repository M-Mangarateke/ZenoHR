// REQ-HR-003, CTL-SARS-001: Domain event published when the EMP201 CSV is downloaded and run is marked Filed.
using ZenoHR.Domain.Events;

namespace ZenoHR.Module.Payroll.Events;

/// <summary>
/// Published when the HR Manager downloads the EMP201 CSV, marking the run as Filed.
/// This is the terminal state — no further transitions are permitted.
/// </summary>
public sealed record PayrollRunFiledEvent(
    string PayrollRunId,
    string Period) : DomainEvent;
