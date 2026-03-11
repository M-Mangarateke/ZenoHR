// CTL-POPIA-009, CTL-POPIA-015, CTL-BCEA-004: Data archival retention policy.
// POPIA §14 — personal data must not be retained beyond its purpose.
// BCEA — employment records must be kept for at least 3 years after termination.
// Policy: 5-year retention post-termination (exceeds BCEA minimum).

namespace ZenoHR.Module.Compliance.Services.DataArchival;

/// <summary>
/// Defines the data retention and archival eligibility rules for terminated employee records.
/// Retention period is <see cref="RetentionYears"/> years from termination date, which
/// satisfies both POPIA §14 (purpose limitation) and BCEA §31 (3-year minimum).
/// </summary>
public sealed class ArchivalPolicy
{
    /// <summary>
    /// Number of years after termination that records are retained before archival eligibility.
    /// </summary>
    public const int RetentionYears = 5;

    /// <summary>
    /// BCEA §31 minimum record retention period in years after termination.
    /// <see cref="RetentionYears"/> must always exceed this value.
    /// </summary>
    public const int BceaMinimumYears = 3;

    /// <summary>
    /// Determines whether a terminated employee's records are eligible for archival.
    /// Records become eligible when the retention period (5 years) has fully elapsed
    /// since the termination date.
    /// </summary>
    /// <param name="terminationDate">The employee's termination date.</param>
    /// <param name="today">The current date for comparison.</param>
    /// <returns><c>true</c> if the retention period has elapsed; otherwise <c>false</c>.</returns>
    public static bool IsEligibleForArchival(DateOnly terminationDate, DateOnly today)
    {
        var expiryDate = GetRetentionExpiryDate(terminationDate);
        return today >= expiryDate;
    }

    /// <summary>
    /// Calculates the date when the retention period expires for a terminated employee.
    /// </summary>
    /// <param name="terminationDate">The employee's termination date.</param>
    /// <returns>The date after which records may be archived.</returns>
    public static DateOnly GetRetentionExpiryDate(DateOnly terminationDate) =>
        terminationDate.AddYears(RetentionYears);
}
