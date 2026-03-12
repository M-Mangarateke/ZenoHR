// REQ-SEC-008: Break-glass emergency access service.
// VUL-006: Manages the full lifecycle — request, dual approval, time-limited access,
// revocation, and mandatory post-event audit review.

using System.Globalization;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Models;

namespace ZenoHR.Module.Compliance.Services;

/// <summary>
/// Service for managing break-glass emergency access lifecycle.
/// Enforces forward-only status transitions, time-limited access windows,
/// and mandatory post-event audit review before closure.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance methods for DI compatibility")]
public sealed class BreakGlassService
{
    private static int _counter;

    /// <summary>Create a new break-glass emergency access request.</summary>
    public Result<BreakGlassRequest> RequestAccess(
        string tenantId,
        string requestedBy,
        string reason,
        BreakGlassUrgency urgency,
        DateTimeOffset requestedAt)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<BreakGlassRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "TenantId is required.");

        if (string.IsNullOrWhiteSpace(requestedBy))
            return Result<BreakGlassRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "RequestedBy is required.");

        if (string.IsNullOrWhiteSpace(reason))
            return Result<BreakGlassRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "Reason is required.");

        if (urgency == BreakGlassUrgency.Unknown)
            return Result<BreakGlassRequest>.Failure(ZenoHrErrorCode.ValidationFailed, "Urgency must be specified (not Unknown).");

        var seq = Interlocked.Increment(ref _counter);
        var requestId = string.Format(CultureInfo.InvariantCulture, "BG-{0}-{1:D4}", requestedAt.Year, seq);

        var request = new BreakGlassRequest
        {
            RequestId = requestId,
            TenantId = tenantId,
            RequestedBy = requestedBy,
            RequestedAt = requestedAt,
            Reason = reason,
            Urgency = urgency,
            Status = BreakGlassStatus.Requested,
        };

        return Result<BreakGlassRequest>.Success(request);
    }

    /// <summary>
    /// Approve a break-glass request — sets a time-limited access window.
    /// Requires Director + SaasAdmin dual approval (caller enforces role check).
    /// </summary>
    public Result<BreakGlassRequest> ApproveAccess(
        BreakGlassRequest existing,
        string approvedBy,
        DateTimeOffset timestamp,
        int expiryHours = BreakGlassRequest.DefaultExpiryHours)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (existing.Status != BreakGlassStatus.Requested)
        {
            return Result<BreakGlassRequest>.Failure(
                ZenoHrErrorCode.InvalidBreakGlassStatusTransition,
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot approve a request in {0} status. Only Requested requests can be approved.",
                    existing.Status));
        }

        if (string.IsNullOrWhiteSpace(approvedBy))
            return Result<BreakGlassRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "ApprovedBy is required.");

        if (expiryHours <= 0)
            return Result<BreakGlassRequest>.Failure(ZenoHrErrorCode.ValueOutOfRange, "ExpiryHours must be greater than zero.");

        var approved = existing with
        {
            Status = BreakGlassStatus.Approved,
            ApprovedBy = approvedBy,
            ApprovedAt = timestamp,
            ExpiresAt = timestamp.AddHours(expiryHours),
        };

        return Result<BreakGlassRequest>.Success(approved);
    }

    /// <summary>Revoke an approved break-glass request immediately.</summary>
    public Result<BreakGlassRequest> RevokeAccess(
        BreakGlassRequest existing,
        string revokedBy,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (existing.Status is not (BreakGlassStatus.Approved or BreakGlassStatus.Active))
        {
            return Result<BreakGlassRequest>.Failure(
                ZenoHrErrorCode.InvalidBreakGlassStatusTransition,
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot revoke a request in {0} status. Only Approved or Active requests can be revoked.",
                    existing.Status));
        }

        if (string.IsNullOrWhiteSpace(revokedBy))
            return Result<BreakGlassRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "RevokedBy is required.");

        var revoked = existing with
        {
            Status = BreakGlassStatus.Revoked,
            RevokedBy = revokedBy,
            RevokedAt = timestamp,
        };

        return Result<BreakGlassRequest>.Success(revoked);
    }

    /// <summary>
    /// Complete the mandatory post-event audit review.
    /// Required before a break-glass request can be closed.
    /// </summary>
    public Result<BreakGlassRequest> CompletePostReview(
        BreakGlassRequest existing,
        string reviewedBy,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (existing.Status is not (BreakGlassStatus.Expired or BreakGlassStatus.Revoked or BreakGlassStatus.PostReviewPending))
        {
            return Result<BreakGlassRequest>.Failure(
                ZenoHrErrorCode.InvalidBreakGlassStatusTransition,
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot complete post-review for a request in {0} status. Request must be Expired, Revoked, or PostReviewPending.",
                    existing.Status));
        }

        if (string.IsNullOrWhiteSpace(reviewedBy))
            return Result<BreakGlassRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "ReviewedBy is required.");

        var reviewed = existing with
        {
            Status = BreakGlassStatus.Closed,
            PostReviewCompletedBy = reviewedBy,
            PostReviewCompletedAt = timestamp,
        };

        return Result<BreakGlassRequest>.Success(reviewed);
    }

    /// <summary>Return all requests that are currently active (approved and not expired).</summary>
    public IReadOnlyList<BreakGlassRequest> GetActiveRequests(IReadOnlyList<BreakGlassRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        return requests.Where(r => r.IsActive).ToList();
    }
}
