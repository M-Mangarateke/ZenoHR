// CTL-POPIA-015: Automated retention enforcement service.
// POPIA §14 — identifies terminated employee records past retention and manages review workflow.
// BCEA §31 — ensures payroll records are retained for the statutory minimum before flagging.

using System.Collections.Immutable;
using System.Globalization;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Module.Compliance.Services.RetentionEnforcement;

/// <summary>
/// Identifies employee records that have exceeded their statutory retention period
/// and manages the review workflow for anonymisation or extended retention.
/// <para>
/// Workflow: IdentifyExpiredRecords → (HR reviews) → ApproveAnonymisation → MarkAnonymised
///           or ExtendRetention (with documented reason).
/// </para>
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance methods for DI compatibility")]
public sealed class RetentionEnforcementService
{
    /// <summary>
    /// Scans a collection of terminated employees and identifies those whose data
    /// has exceeded the retention period for each applicable data category.
    /// </summary>
    /// <param name="employees">
    /// Terminated employees to evaluate. Each tuple contains:
    /// (TenantId, EmployeeId, TerminationDate, DataCategories).
    /// </param>
    /// <param name="currentDate">The current date for retention calculation.</param>
    /// <returns>A list of <see cref="RetentionReviewRecord"/> for all expired data sets.</returns>
    public IReadOnlyList<RetentionReviewRecord> IdentifyExpiredRecords(
        IEnumerable<(string TenantId, string EmployeeId, DateTimeOffset TerminationDate, IEnumerable<DataCategory> DataCategories)> employees,
        DateTimeOffset currentDate)
    {
        ArgumentNullException.ThrowIfNull(employees);

        var results = ImmutableList.CreateBuilder<RetentionReviewRecord>();

        foreach (var (tenantId, employeeId, terminationDate, dataCategories) in employees)
        {
            foreach (var category in dataCategories)
            {
                var retentionYears = RetentionPolicy.DetermineRetentionYears(category);

                if (!RetentionPolicy.IsRetentionExpired(terminationDate, retentionYears, currentDate))
                {
                    continue;
                }

                var expiryDate = RetentionPolicy.GetRetentionExpiryDate(terminationDate, retentionYears);

                var review = new RetentionReviewRecord(
                    ReviewId: string.Format(CultureInfo.InvariantCulture, "RET-{0}-{1}-{2}", tenantId, employeeId, category),
                    TenantId: tenantId,
                    EmployeeId: employeeId,
                    DataCategory: category,
                    TerminationDate: terminationDate,
                    RetentionExpiryDate: expiryDate,
                    Status: RetentionReviewStatus.Pending);

                results.Add(review);
            }
        }

        return results.ToImmutable();
    }

    /// <summary>
    /// Approves a pending retention review for anonymisation.
    /// Transitions: Pending → Approved.
    /// </summary>
    /// <param name="review">The review to approve.</param>
    /// <param name="approvedBy">Identity of the approver (typically HR Manager).</param>
    /// <returns>A <see cref="Result{T}"/> containing the updated review or an error.</returns>
    public Result<RetentionReviewRecord> ApproveAnonymisation(RetentionReviewRecord review, string approvedBy)
    {
        ArgumentNullException.ThrowIfNull(review);
        ArgumentException.ThrowIfNullOrWhiteSpace(approvedBy);

        if (review.Status != RetentionReviewStatus.Pending)
        {
            return Result<RetentionReviewRecord>.Failure(
                ZenoHrErrorCode.ComplianceCheckFailed,
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot approve review {0}: status is {1}, expected Pending.", review.ReviewId, review.Status));
        }

        return Result<RetentionReviewRecord>.Success(review with
        {
            Status = RetentionReviewStatus.Approved,
            ReviewedBy = approvedBy,
            ReviewedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Marks an approved retention review as anonymised (irreversible).
    /// Transitions: Approved → Anonymised.
    /// </summary>
    /// <param name="review">The review to mark as anonymised.</param>
    /// <returns>A <see cref="Result{T}"/> containing the updated review or an error.</returns>
    public Result<RetentionReviewRecord> MarkAnonymised(RetentionReviewRecord review)
    {
        ArgumentNullException.ThrowIfNull(review);

        if (review.Status != RetentionReviewStatus.Approved)
        {
            return Result<RetentionReviewRecord>.Failure(
                ZenoHrErrorCode.ComplianceCheckFailed,
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot anonymise review {0}: status is {1}, expected Approved.", review.ReviewId, review.Status));
        }

        return Result<RetentionReviewRecord>.Success(review with
        {
            Status = RetentionReviewStatus.Anonymised,
            ReviewedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Extends the retention period for a record with a documented reason.
    /// Transitions: Pending → Retained.
    /// </summary>
    /// <param name="review">The review to extend.</param>
    /// <param name="reason">Documented reason for extending retention (e.g., "Pending litigation").</param>
    /// <param name="extendedBy">Identity of the person extending retention.</param>
    /// <returns>A <see cref="Result{T}"/> containing the updated review or an error.</returns>
    public Result<RetentionReviewRecord> ExtendRetention(RetentionReviewRecord review, string reason, string extendedBy)
    {
        ArgumentNullException.ThrowIfNull(review);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(extendedBy);

        if (review.Status != RetentionReviewStatus.Pending)
        {
            return Result<RetentionReviewRecord>.Failure(
                ZenoHrErrorCode.ComplianceCheckFailed,
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot extend retention for review {0}: status is {1}, expected Pending.", review.ReviewId, review.Status));
        }

        return Result<RetentionReviewRecord>.Success(review with
        {
            Status = RetentionReviewStatus.Retained,
            ReviewedBy = extendedBy,
            ReviewedAt = DateTimeOffset.UtcNow,
            RetentionReason = reason
        });
    }
}
