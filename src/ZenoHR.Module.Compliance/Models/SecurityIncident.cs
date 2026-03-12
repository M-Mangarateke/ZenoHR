// CTL-POPIA-008: Security incident record for breach detection and anomaly monitoring.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Immutable record representing a detected security incident.
/// Created by <see cref="Services.AnomalyDetectionService"/> when anomalous behaviour
/// exceeds configured alert thresholds (PRD-05 §7).
/// </summary>
public sealed record SecurityIncident
{
    public required string IncidentId { get; init; }
    public required string TenantId { get; init; }
    public required DateTimeOffset DetectedAt { get; init; }
    public required BreachSeverity Severity { get; init; }
    public required SecurityIncidentType IncidentType { get; init; }
    public required string Description { get; init; }
    public string? AffectedUserId { get; init; }
    public string? SourceIp { get; init; }
    public required IncidentStatus Status { get; init; }
    public string? ResolvedBy { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public string? Notes { get; init; }
}
