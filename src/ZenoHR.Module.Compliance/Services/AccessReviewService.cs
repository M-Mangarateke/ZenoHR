// CTL-POPIA-007: Monthly access review service — PRD-15 §9.
// Generates review records, detects stale/elevated/orphaned role assignments,
// and manages approval/rejection lifecycle.

using System.Globalization;
using System.Threading;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Models;

namespace ZenoHR.Module.Compliance.Services;

/// <summary>
/// Generates and manages monthly access review records per CTL-POPIA-007.
/// Detects stale assignments (>90 days without login), elevated privileges,
/// and terminated employees with active roles.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance methods for DI compatibility")]
public sealed class AccessReviewService
{
    private static readonly string[] ElevatedRoles = ["Director", "HRManager", "SaasAdmin"];
    private static readonly TimeSpan StaleLoginThreshold = TimeSpan.FromDays(90);

    private static int _counter;

    /// <summary>
    /// Generate an access review for the given tenant and period.
    /// Analyses all role assignments to detect stale, elevated, and orphaned entries.
    /// </summary>
    public Result<AccessReviewRecord> GenerateReview(
        string tenantId,
        string period,
        IReadOnlyList<RoleAssignmentEntry> assignments)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<AccessReviewRecord>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "TenantId is required.");

        if (string.IsNullOrWhiteSpace(period))
            return Result<AccessReviewRecord>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "ReviewPeriod is required.");

        ArgumentNullException.ThrowIfNull(assignments);

        var now = DateTimeOffset.UtcNow;
        var findings = DetectFindings(assignments, now);

        var seq = Interlocked.Increment(ref _counter);
        var reviewId = string.Format(CultureInfo.InvariantCulture, "AR-{0}-{1:D4}", period, seq);

        var record = new AccessReviewRecord
        {
            ReviewId = reviewId,
            TenantId = tenantId,
            ReviewPeriod = period,
            GeneratedAt = now,
            Status = AccessReviewStatus.Pending,
            TotalAssignments = assignments.Count,
            Findings = findings,
        };

        return Result<AccessReviewRecord>.Success(record);
    }

    /// <summary>Approve a pending or in-review access review.</summary>
    public Result<AccessReviewRecord> ApproveReview(
        AccessReviewRecord existing,
        string reviewedBy,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (string.IsNullOrWhiteSpace(reviewedBy))
            return Result<AccessReviewRecord>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "ReviewedBy is required.");

        if (existing.Status is not (AccessReviewStatus.Pending or AccessReviewStatus.InReview))
        {
            return Result<AccessReviewRecord>.Failure(
                ZenoHrErrorCode.InvalidAccessReviewStatusTransition,
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot approve review in status {0}. Must be Pending or InReview.", existing.Status));
        }

        var approved = existing with
        {
            Status = AccessReviewStatus.Approved,
            ReviewedBy = reviewedBy,
            ReviewedAt = timestamp,
        };

        return Result<AccessReviewRecord>.Success(approved);
    }

    /// <summary>Reject a pending or in-review access review with a reason.</summary>
    public Result<AccessReviewRecord> RejectReview(
        AccessReviewRecord existing,
        string reviewedBy,
        string reason,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (string.IsNullOrWhiteSpace(reviewedBy))
            return Result<AccessReviewRecord>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "ReviewedBy is required.");

        if (string.IsNullOrWhiteSpace(reason))
            return Result<AccessReviewRecord>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "Rejection reason is required.");

        if (existing.Status is not (AccessReviewStatus.Pending or AccessReviewStatus.InReview))
        {
            return Result<AccessReviewRecord>.Failure(
                ZenoHrErrorCode.InvalidAccessReviewStatusTransition,
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot reject review in status {0}. Must be Pending or InReview.", existing.Status));
        }

        var rejected = existing with
        {
            Status = AccessReviewStatus.Rejected,
            ReviewedBy = reviewedBy,
            ReviewedAt = timestamp,
        };

        return Result<AccessReviewRecord>.Success(rejected);
    }

    // ── Finding Detection ──────────────────────────────────────────────────

    private static List<AccessReviewFinding> DetectFindings(
        IReadOnlyList<RoleAssignmentEntry> assignments,
        DateTimeOffset now)
    {
        var findings = new List<AccessReviewFinding>();

        foreach (var assignment in assignments)
        {
            // Terminated employees with active role assignments
            if (assignment.IsTerminated)
            {
                findings.Add(new AccessReviewFinding
                {
                    EmployeeId = assignment.EmployeeId,
                    RoleName = assignment.RoleName,
                    DepartmentId = assignment.DepartmentId,
                    AssignedAt = assignment.AssignedAt,
                    FindingType = "TERMINATED_WITH_ACTIVE_ROLE",
                    Recommendation = "Revoke role assignment immediately — employee is terminated.",
                });
                continue; // Terminated finding takes precedence; skip other checks for this entry
            }

            // Stale assignment: no login in >90 days
            if (assignment.LastLoginAt is null ||
                (now - assignment.LastLoginAt.Value) > StaleLoginThreshold)
            {
                findings.Add(new AccessReviewFinding
                {
                    EmployeeId = assignment.EmployeeId,
                    RoleName = assignment.RoleName,
                    DepartmentId = assignment.DepartmentId,
                    AssignedAt = assignment.AssignedAt,
                    FindingType = "NO_RECENT_LOGIN",
                    Recommendation = "Review assignment — no login in over 90 days.",
                });
            }

            // Elevated privilege check
            if (ElevatedRoles.Contains(assignment.RoleName, StringComparer.OrdinalIgnoreCase))
            {
                findings.Add(new AccessReviewFinding
                {
                    EmployeeId = assignment.EmployeeId,
                    RoleName = assignment.RoleName,
                    DepartmentId = assignment.DepartmentId,
                    AssignedAt = assignment.AssignedAt,
                    FindingType = "ELEVATED_PRIVILEGE",
                    Recommendation = "Verify elevated role is still required and follows least-privilege principle.",
                });
            }
        }

        return findings;
    }
}
