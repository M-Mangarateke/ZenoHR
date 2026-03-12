// REQ-SEC-008: Break-glass emergency access urgency classification.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Urgency classification for break-glass emergency access requests.
/// Determines escalation priority and audit scrutiny level.
/// </summary>
public enum BreakGlassUrgency
{
    Unknown = 0,
    PayrollCrisis = 1,
    SecurityBreach = 2,
    SystemOutage = 3,
    RegulatoryDeadline = 4,
}
