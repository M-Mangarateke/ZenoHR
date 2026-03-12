// CTL-POPIA-007: Monthly access review record — PRD-15 §9 mandates Director/HRManager approval.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Immutable record of a monthly access review. Generated automatically on the 1st of each month.
/// Must be reviewed and approved/rejected by a Director or HRManager.
/// </summary>
public sealed record AccessReviewRecord
{
    public required string ReviewId { get; init; }
    public required string TenantId { get; init; }

    /// <summary>Review period in "yyyy-MM" format (e.g. "2026-03").</summary>
    public required string ReviewPeriod { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }
    public string? ReviewedBy { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
    public required AccessReviewStatus Status { get; init; }
    public required int TotalAssignments { get; init; }
    public required IReadOnlyList<AccessReviewFinding> Findings { get; init; }
}
