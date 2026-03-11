// CTL-POPIA-010, CTL-POPIA-011: POPIA breach register record.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Immutable record representing a personal information security breach.
/// POPIA Act §22 requires notification to the Information Regulator within 72 hours.
/// </summary>
public sealed record BreachRecord
{
    public required string BreachId { get; init; }
    public required string TenantId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required BreachSeverity Severity { get; init; }
    public required BreachStatus Status { get; init; }
    public required DateTimeOffset DiscoveredAt { get; init; }
    public DateTimeOffset? ContainedAt { get; init; }
    public DateTimeOffset? RegulatorNotifiedAt { get; init; }
    public DateTimeOffset? SubjectsNotifiedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public required string DiscoveredBy { get; init; }
    public required string[] AffectedDataCategories { get; init; }
    public required int EstimatedAffectedSubjects { get; init; }
    public required string RootCause { get; init; }
    public required string[] RemediationSteps { get; init; }
    public string? RegulatorReferenceNumber { get; init; }
    public string? Notes { get; init; }

    /// <summary>72-hour deadline for Information Regulator notification (POPIA §22).</summary>
    public DateTimeOffset NotificationDeadline => DiscoveredAt.AddHours(72);

    /// <summary>True if past 72-hour deadline and regulator has not been notified.</summary>
    public bool IsOverdue => Status < BreachStatus.RegulatorNotified && DateTimeOffset.UtcNow > NotificationDeadline;
}
