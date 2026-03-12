// CTL-POPIA-007: Individual finding from a monthly access review.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Represents a single finding discovered during a monthly access review —
/// e.g. stale role assignments, elevated privileges, or terminated employees with active roles.
/// </summary>
public sealed record AccessReviewFinding
{
    public required string EmployeeId { get; init; }
    public required string RoleName { get; init; }
    public required string DepartmentId { get; init; }
    public required DateTimeOffset AssignedAt { get; init; }

    /// <summary>
    /// Type of finding. Well-known values: STALE_ASSIGNMENT, ELEVATED_PRIVILEGE,
    /// NO_RECENT_LOGIN, TERMINATED_WITH_ACTIVE_ROLE.
    /// </summary>
    public required string FindingType { get; init; }

    /// <summary>Recommended remediation action for this finding.</summary>
    public required string Recommendation { get; init; }
}
