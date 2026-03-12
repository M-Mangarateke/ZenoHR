// CTL-POPIA-007: Input DTO for access review — represents a single user-role assignment.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Represents a single role assignment entry used as input for access review generation.
/// Carries the data needed to detect stale, elevated, or orphaned assignments.
/// </summary>
public sealed record RoleAssignmentEntry
{
    public required string EmployeeId { get; init; }
    public required string RoleName { get; init; }
    public required string DepartmentId { get; init; }
    public required DateTimeOffset AssignedAt { get; init; }
    public DateTimeOffset? LastLoginAt { get; init; }
    public bool IsTerminated { get; init; }
}
