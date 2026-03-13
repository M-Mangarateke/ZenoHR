// CTL-POPIA-015: Retention review record — tracks each data set flagged for review.
// POPIA §14 — personal data must not be retained beyond its purpose.

namespace ZenoHR.Module.Compliance.Services.RetentionEnforcement;

/// <summary>
/// Represents a single retention review for an employee's data that has exceeded
/// the statutory retention period. Created by the retention enforcement service
/// and processed through the review workflow (Pending → Approved → Anonymised, or Retained).
/// </summary>
/// <param name="ReviewId">Unique identifier for this review.</param>
/// <param name="TenantId">Tenant scope — all queries must filter by this (Critical Rule 5).</param>
/// <param name="EmployeeId">The employee whose data is under review.</param>
/// <param name="DataCategory">The category of data determining the retention period.</param>
/// <param name="TerminationDate">The employee's termination date.</param>
/// <param name="RetentionExpiryDate">The date when the retention period expired.</param>
/// <param name="Status">Current workflow status of this review.</param>
/// <param name="ReviewedBy">Identity of the reviewer (null if not yet reviewed).</param>
/// <param name="ReviewedAt">Timestamp of the review action (null if not yet reviewed).</param>
/// <param name="RetentionReason">Reason for extending retention (populated only when Status is Retained).</param>
public sealed record RetentionReviewRecord(
    string ReviewId,
    string TenantId,
    string EmployeeId,
    DataCategory DataCategory,
    DateTimeOffset TerminationDate,
    DateTimeOffset RetentionExpiryDate,
    RetentionReviewStatus Status,
    string? ReviewedBy = null,
    DateTimeOffset? ReviewedAt = null,
    string? RetentionReason = null);
