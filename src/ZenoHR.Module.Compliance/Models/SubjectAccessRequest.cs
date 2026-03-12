// CTL-POPIA-009: POPIA §23 Data Subject Access Request record.
// Employees have the right to request a copy of all personal information held.
// Responses required within 30 calendar days.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Immutable record representing a POPIA §23 Subject Access Request (SAR).
/// Tracks the lifecycle from submission through data gathering to completion or rejection.
/// </summary>
public sealed record SubjectAccessRequest
{
    public required string RequestId { get; init; }
    public required string TenantId { get; init; }
    public required string EmployeeId { get; init; }
    public required DateTimeOffset RequestedAt { get; init; }
    public required string RequestedBy { get; init; }
    public required SarStatus Status { get; init; }

    /// <summary>30 calendar day deadline from request date (POPIA §23).</summary>
    public required DateOnly DeadlineDate { get; init; }

    public string? ReviewedBy { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? RejectionReason { get; init; }
    public DateTimeOffset? DataPackageGeneratedAt { get; init; }

    /// <summary>True if past 30-day deadline and request has not been completed or rejected.</summary>
    public bool IsOverdue => Status < SarStatus.Completed && DateOnly.FromDateTime(DateTime.UtcNow) > DeadlineDate;

    /// <summary>Days remaining until the 30-day deadline (negative if overdue).</summary>
    public int DaysRemaining => (DeadlineDate.ToDateTime(TimeOnly.MinValue) - DateTime.UtcNow).Days;
}
