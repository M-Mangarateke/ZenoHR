// CTL-POPIA-012, REQ-SEC-009: Incident response record — full lifecycle from detection to closure.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Immutable record representing an incident response lifecycle.
/// Links to a <see cref="SecurityIncident"/> and tracks containment, investigation,
/// recovery, post-review, and closure with documented evidence at each step.
/// </summary>
public sealed record IncidentResponse
{
    public required string IncidentId { get; init; }
    public required string TenantId { get; init; }
    public required string SecurityIncidentId { get; init; }
    public required IncidentSeverityLevel Severity { get; init; }
    public required IncidentResponseStatus Status { get; init; }

    public required string ClassifiedBy { get; init; }
    public required DateTimeOffset ClassifiedAt { get; init; }

    public IReadOnlyList<ContainmentAction> ContainmentActions { get; init; } = [];

    public string? InvestigationNotes { get; init; }

    public string? RecoveredBy { get; init; }
    public DateTimeOffset? RecoveredAt { get; init; }

    public string? PostReviewNotes { get; init; }
    public string? ReviewedBy { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }

    public string? ClosedBy { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }

    public bool EvidencePackGenerated { get; init; }
}
