// CTL-POPIA-015: Retention policy — determines retention periods and expiry.
// POPIA §14 — personal data must not be retained beyond its purpose.
// BCEA §31 — payroll records must be kept for at least 3 years after termination.

using System.Globalization;

namespace ZenoHR.Module.Compliance.Services.RetentionEnforcement;

/// <summary>
/// Defines statutory retention periods for different data categories and provides
/// methods to determine whether retention has expired. All retention periods are
/// measured from the employee's termination date.
/// <para>
/// Retention hierarchy:
/// <list type="bullet">
///   <item><description>Payroll: 3 years (BCEA §31 minimum)</description></item>
///   <item><description>General/Leave/TimeAttendance: 5 years (POPIA §14 default)</description></item>
///   <item><description>AuditTrail/ComplianceSubmission: 7 years (tamper-evidence requirement)</description></item>
/// </list>
/// </para>
/// </summary>
public sealed class RetentionPolicy
{
    /// <summary>BCEA §31 — payroll records minimum retention period in years.</summary>
    public const int BceaPayrollRetentionYears = 3;

    /// <summary>POPIA §14 — default retention period for general personal information.</summary>
    public const int PopiaDefaultRetentionYears = 5;

    /// <summary>Audit trail retention — extended period for hash-chained tamper-evidence records.</summary>
    public const int AuditTrailRetentionYears = 7;

    /// <summary>
    /// Determines whether the retention period has expired for a given termination date.
    /// </summary>
    /// <param name="terminationDate">The employee's termination date.</param>
    /// <param name="retentionYears">The applicable retention period in years.</param>
    /// <param name="currentDate">The current date for comparison.</param>
    /// <returns><c>true</c> if the retention period has fully elapsed; otherwise <c>false</c>.</returns>
    public static bool IsRetentionExpired(DateTimeOffset terminationDate, int retentionYears, DateTimeOffset currentDate)
    {
        var expiryDate = GetRetentionExpiryDate(terminationDate, retentionYears);
        return currentDate >= expiryDate;
    }

    /// <summary>
    /// Calculates the date when the retention period expires.
    /// </summary>
    /// <param name="terminationDate">The employee's termination date.</param>
    /// <param name="retentionYears">The applicable retention period in years.</param>
    /// <returns>The <see cref="DateTimeOffset"/> after which data may be reviewed for anonymisation.</returns>
    public static DateTimeOffset GetRetentionExpiryDate(DateTimeOffset terminationDate, int retentionYears) =>
        terminationDate.AddYears(retentionYears);

    /// <summary>
    /// Determines the applicable retention period in years for a given data category.
    /// </summary>
    /// <param name="dataCategory">The category of data.</param>
    /// <returns>The retention period in years.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for <see cref="DataCategory.Unknown"/>.</exception>
    public static int DetermineRetentionYears(DataCategory dataCategory) =>
        dataCategory switch
        {
            DataCategory.Payroll => BceaPayrollRetentionYears,
            DataCategory.General => PopiaDefaultRetentionYears,
            DataCategory.Leave => PopiaDefaultRetentionYears,
            DataCategory.TimeAttendance => PopiaDefaultRetentionYears,
            DataCategory.AuditTrail => AuditTrailRetentionYears,
            DataCategory.ComplianceSubmission => AuditTrailRetentionYears,
            DataCategory.Unknown => throw new ArgumentOutOfRangeException(
                nameof(dataCategory),
                dataCategory,
                string.Format(CultureInfo.InvariantCulture, "Cannot determine retention period for {0}.", dataCategory)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(dataCategory),
                dataCategory,
                string.Format(CultureInfo.InvariantCulture, "Unrecognised data category: {0}.", dataCategory))
        };
}
