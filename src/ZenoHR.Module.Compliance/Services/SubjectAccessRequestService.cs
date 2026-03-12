// CTL-POPIA-009: POPIA §23 Subject Access Request workflow service.
// Manages SAR submission, status transitions, deadline tracking, and overdue detection.

using System.Globalization;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Models;

namespace ZenoHR.Module.Compliance.Services;

/// <summary>
/// Service for managing POPIA §23 Subject Access Request lifecycle —
/// submission, review, data gathering, completion/rejection, and overdue detection.
/// Responses must be provided within 30 calendar days.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance methods for DI compatibility")]
public sealed class SubjectAccessRequestService
{
    private static int _counter;

    /// <summary>Submit a new Subject Access Request (POPIA §23).</summary>
    // CTL-POPIA-009
    public Result<SubjectAccessRequest> SubmitRequest(
        string tenantId,
        string employeeId,
        string requestedBy,
        DateTimeOffset requestedAt)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<SubjectAccessRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "TenantId is required.");

        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<SubjectAccessRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "EmployeeId is required.");

        if (string.IsNullOrWhiteSpace(requestedBy))
            return Result<SubjectAccessRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "RequestedBy is required.");

        var seq = Interlocked.Increment(ref _counter);
        var requestId = string.Format(CultureInfo.InvariantCulture, "SAR-{0}-{1:D4}", requestedAt.Year, seq);
        var deadlineDate = DateOnly.FromDateTime(requestedAt.UtcDateTime).AddDays(30);

        var request = new SubjectAccessRequest
        {
            RequestId = requestId,
            TenantId = tenantId,
            EmployeeId = employeeId,
            RequestedAt = requestedAt,
            RequestedBy = requestedBy,
            Status = SarStatus.Submitted,
            DeadlineDate = deadlineDate
        };

        return Result<SubjectAccessRequest>.Success(request);
    }

    /// <summary>Move SAR to UnderReview status.</summary>
    // CTL-POPIA-009
    public Result<SubjectAccessRequest> ReviewRequest(
        SubjectAccessRequest existing,
        string reviewedBy,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (string.IsNullOrWhiteSpace(reviewedBy))
            return Result<SubjectAccessRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "ReviewedBy is required.");

        var transitionResult = ValidateForwardTransition(existing.Status, SarStatus.UnderReview);
        if (transitionResult.IsFailure)
            return Result<SubjectAccessRequest>.Failure(transitionResult.Error);

        var updated = existing with
        {
            Status = SarStatus.UnderReview,
            ReviewedBy = reviewedBy,
            ReviewedAt = timestamp
        };

        return Result<SubjectAccessRequest>.Success(updated);
    }

    /// <summary>Complete a SAR — data package has been generated and delivered.</summary>
    // CTL-POPIA-009
    public Result<SubjectAccessRequest> CompleteRequest(
        SubjectAccessRequest existing,
        string completedBy,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (string.IsNullOrWhiteSpace(completedBy))
            return Result<SubjectAccessRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "CompletedBy is required.");

        var transitionResult = ValidateForwardTransition(existing.Status, SarStatus.Completed);
        if (transitionResult.IsFailure)
            return Result<SubjectAccessRequest>.Failure(transitionResult.Error);

        var updated = existing with
        {
            Status = SarStatus.Completed,
            CompletedAt = timestamp,
            DataPackageGeneratedAt = timestamp
        };

        return Result<SubjectAccessRequest>.Success(updated);
    }

    /// <summary>Reject a SAR with a mandatory reason.</summary>
    // CTL-POPIA-009
    public Result<SubjectAccessRequest> RejectRequest(
        SubjectAccessRequest existing,
        string rejectedBy,
        string reason,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (string.IsNullOrWhiteSpace(rejectedBy))
            return Result<SubjectAccessRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "RejectedBy is required.");

        if (string.IsNullOrWhiteSpace(reason))
            return Result<SubjectAccessRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "RejectionReason is required.");

        var transitionResult = ValidateForwardTransition(existing.Status, SarStatus.Rejected);
        if (transitionResult.IsFailure)
            return Result<SubjectAccessRequest>.Failure(transitionResult.Error);

        var updated = existing with
        {
            Status = SarStatus.Rejected,
            ReviewedBy = rejectedBy,
            ReviewedAt = timestamp,
            RejectionReason = reason
        };

        return Result<SubjectAccessRequest>.Success(updated);
    }

    /// <summary>Return SARs that have exceeded the 30-day POPIA §23 deadline.</summary>
    // CTL-POPIA-009
    public IReadOnlyList<SubjectAccessRequest> GetOverdueRequests(IReadOnlyList<SubjectAccessRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        return requests.Where(r => r.IsOverdue).ToList();
    }

    /// <summary>
    /// Validate forward-only status transitions.
    /// Completed (4) and Rejected (5) are both terminal states reachable from earlier statuses.
    /// Backward transitions are not allowed.
    /// </summary>
    private static Result ValidateForwardTransition(SarStatus current, SarStatus target)
    {
        // Rejected is a special terminal state — allowed from any non-terminal status
        if (target == SarStatus.Rejected)
        {
            if (current >= SarStatus.Completed)
            {
                return Result.Failure(
                    ZenoHrErrorCode.InvalidSarStatusTransition,
                    string.Format(CultureInfo.InvariantCulture,
                        "Cannot transition from {0} to {1}. Request is already in a terminal state.",
                        current, target));
            }

            return Result.Success();
        }

        // For non-rejection transitions, enforce strictly forward movement
        if (target <= current)
        {
            return Result.Failure(
                ZenoHrErrorCode.InvalidSarStatusTransition,
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot transition from {0} to {1}. Status can only move forward.",
                    current, target));
        }

        return Result.Success();
    }
}
