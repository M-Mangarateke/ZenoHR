// CTL-POPIA-010: POPIA §24 — correction of personal information workflow service.
// Manages submission, review, approval/rejection, and application of correction requests.

using System.Globalization;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Models;

namespace ZenoHR.Module.Compliance.Services;

/// <summary>
/// Service for managing POPIA §24 correction request lifecycle — submission,
/// review, approval/rejection, and application tracking.
/// Actual data mutation is handled by the caller; this service manages the request state machine.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance methods for DI compatibility")]
public sealed class CorrectionRequestService
{
    /// <summary>Submit a new correction request from a data subject.</summary>
    // CTL-POPIA-010
    public Result<CorrectionRequest> SubmitCorrection(
        string tenantId,
        string employeeId,
        string fieldName,
        string currentValue,
        string proposedValue,
        string reason,
        string requestedBy,
        DateTimeOffset requestedAt)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<CorrectionRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "TenantId is required.");

        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<CorrectionRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "EmployeeId is required.");

        if (string.IsNullOrWhiteSpace(fieldName))
            return Result<CorrectionRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "FieldName is required.");

        if (string.IsNullOrWhiteSpace(reason))
            return Result<CorrectionRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "Reason is required.");

        if (string.IsNullOrWhiteSpace(requestedBy))
            return Result<CorrectionRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "RequestedBy is required.");

        if (string.Equals(currentValue, proposedValue, StringComparison.Ordinal))
            return Result<CorrectionRequest>.Failure(ZenoHrErrorCode.ValidationFailed, "ProposedValue must differ from CurrentValue.");

        var requestId = string.Format(CultureInfo.InvariantCulture, "COR-{0}-{1}", requestedAt.Year, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8]);

        var request = new CorrectionRequest
        {
            RequestId = requestId,
            TenantId = tenantId,
            EmployeeId = employeeId,
            RequestedAt = requestedAt,
            RequestedBy = requestedBy,
            FieldName = fieldName,
            CurrentValue = currentValue ?? string.Empty,
            ProposedValue = proposedValue ?? string.Empty,
            Reason = reason,
            Status = CorrectionStatus.Submitted
        };

        return Result<CorrectionRequest>.Success(request);
    }

    /// <summary>Approve a correction request (transition from UnderReview to Approved).</summary>
    // CTL-POPIA-010
    public Result<CorrectionRequest> ApproveCorrection(
        CorrectionRequest existing,
        string reviewedBy,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (string.IsNullOrWhiteSpace(reviewedBy))
            return Result<CorrectionRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "ReviewedBy is required.");

        var transitionResult = ValidateForwardTransition(existing.Status, CorrectionStatus.Approved);
        if (transitionResult.IsFailure)
            return Result<CorrectionRequest>.Failure(transitionResult.Error);

        var updated = existing with
        {
            Status = CorrectionStatus.Approved,
            ReviewedBy = reviewedBy,
            ReviewedAt = timestamp
        };

        return Result<CorrectionRequest>.Success(updated);
    }

    /// <summary>
    /// Mark a correction as applied (transition from Approved to Applied).
    /// The actual data change is handled by the caller.
    /// </summary>
    // CTL-POPIA-010
    public Result<CorrectionRequest> ApplyCorrection(
        CorrectionRequest existing,
        DateTimeOffset appliedAt)
    {
        ArgumentNullException.ThrowIfNull(existing);

        var transitionResult = ValidateForwardTransition(existing.Status, CorrectionStatus.Applied);
        if (transitionResult.IsFailure)
            return Result<CorrectionRequest>.Failure(transitionResult.Error);

        var updated = existing with
        {
            Status = CorrectionStatus.Applied,
            AppliedAt = appliedAt
        };

        return Result<CorrectionRequest>.Success(updated);
    }

    /// <summary>Reject a correction request with a mandatory reason.</summary>
    // CTL-POPIA-010
    public Result<CorrectionRequest> RejectCorrection(
        CorrectionRequest existing,
        string reviewedBy,
        string rejectionReason,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (string.IsNullOrWhiteSpace(reviewedBy))
            return Result<CorrectionRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "ReviewedBy is required.");

        if (string.IsNullOrWhiteSpace(rejectionReason))
            return Result<CorrectionRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "RejectionReason is required when rejecting a correction request.");

        var transitionResult = ValidateForwardTransition(existing.Status, CorrectionStatus.Rejected);
        if (transitionResult.IsFailure)
            return Result<CorrectionRequest>.Failure(transitionResult.Error);

        var updated = existing with
        {
            Status = CorrectionStatus.Rejected,
            ReviewedBy = reviewedBy,
            ReviewedAt = timestamp,
            RejectionReason = rejectionReason
        };

        return Result<CorrectionRequest>.Success(updated);
    }

    /// <summary>
    /// Validates that a status transition follows the allowed state machine.
    /// Main path: Submitted → UnderReview → Approved → Applied.
    /// Branch: Submitted or UnderReview → Rejected (terminal).
    /// Each transition must follow exactly one step — no skipping states.
    /// </summary>
    private static Result ValidateForwardTransition(CorrectionStatus current, CorrectionStatus target)
    {
        var isValid = (current, target) switch
        {
            (CorrectionStatus.Submitted, CorrectionStatus.UnderReview) => true,
            (CorrectionStatus.UnderReview, CorrectionStatus.Approved) => true,
            (CorrectionStatus.Approved, CorrectionStatus.Applied) => true,
            (CorrectionStatus.Submitted, CorrectionStatus.Rejected) => true,
            (CorrectionStatus.UnderReview, CorrectionStatus.Rejected) => true,
            _ => false
        };

        if (!isValid)
        {
            return Result.Failure(
                ZenoHrErrorCode.InvalidBreachStatusTransition,
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot transition from {0} to {1}. Invalid status transition.",
                    current, target));
        }

        return Result.Success();
    }
}
