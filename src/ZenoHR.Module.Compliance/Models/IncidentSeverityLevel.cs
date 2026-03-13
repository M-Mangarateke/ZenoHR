// CTL-POPIA-012, REQ-SEC-009: Incident severity classification for incident response.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Severity levels for incident response classification.
/// </summary>
public enum IncidentSeverityLevel
{
    Unknown = 0,
    Sev1Critical = 1,
    Sev2High = 2,
    Sev3Medium = 3,
    Sev4Low = 4
}
