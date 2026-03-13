// VUL-024, VUL-025: BCEA pre-payroll compliance checks.
// CTL-BCEA-001: Ordinary hours limits (45/week max).
// CTL-BCEA-003: Leave entitlement minimums (15 days/year, pro-rated 1.25 days/month).

using ZenoHR.Domain.Errors;
using ZenoHR.Module.Payroll.Models;

namespace ZenoHR.Module.Payroll.Services;

/// <summary>
/// Validates BCEA working-time and leave compliance before payroll finalization.
/// Overtime violations block payroll; leave balance warnings do not.
/// </summary>
public sealed class BceaComplianceCheckService
{
    // CTL-BCEA-001: BCEA Section 9 — ordinary hours
    private const decimal MaxOrdinaryHoursPerWeek = 45m;

    // CTL-BCEA-001: BCEA Section 10 — overtime (with written agreement)
    private const decimal MaxOvertimeHoursPerWeek = 10m;

    // CTL-BCEA-001: Total max = ordinary + overtime
    private const decimal MaxTotalHoursWithAgreement = MaxOrdinaryHoursPerWeek + MaxOvertimeHoursPerWeek; // 55

    // CTL-BCEA-003: BCEA Section 20 — annual leave entitlement
    private const decimal AnnualLeaveEntitlementDays = 15m;
    private const decimal MonthlyLeaveAccrualRate = AnnualLeaveEntitlementDays / 12m; // 1.25

    /// <summary>
    /// Checks overtime compliance against BCEA working-time limits.
    /// </summary>
    /// <param name="weeklyHours">Total hours worked in the week (ordinary + overtime).</param>
    /// <param name="isOvertimeAgreed">Whether a written overtime agreement exists.</param>
    /// <returns>Result containing compliance result, or failure on invalid input.</returns>
    public static Result<BceaComplianceResult> CheckOvertimeCompliance(decimal weeklyHours, bool isOvertimeAgreed)
    {
        // VUL-024: Validate input
        if (weeklyHours < 0)
        {
            var violations = new List<string> { "Weekly hours cannot be negative." };
            return Result<BceaComplianceResult>.Success(
                new BceaComplianceResult(violations, Array.Empty<string>()));
        }

        var violationsList = new List<string>();

        if (!isOvertimeAgreed)
        {
            // Without overtime agreement, maximum is ordinary hours only (45)
            if (weeklyHours > MaxOrdinaryHoursPerWeek)
            {
                // Determine if it's an overtime issue or ordinary hours issue
                violationsList.Add(
                    $"No overtime agreement in place. Employee worked {weeklyHours}h but maximum without agreement is {MaxOrdinaryHoursPerWeek}h/week. " +
                    "A written overtime agreement is required under BCEA Section 10.");
            }
        }
        else
        {
            // With overtime agreement, ordinary limit still applies + overtime capped at 10h
            if (weeklyHours > MaxTotalHoursWithAgreement)
            {
                violationsList.Add(
                    $"Total weekly hours ({weeklyHours}h) exceed BCEA maximum of {MaxTotalHoursWithAgreement}h/week " +
                    $"(ordinary {MaxOrdinaryHoursPerWeek}h + overtime {MaxOvertimeHoursPerWeek}h).");
            }
        }

        return Result<BceaComplianceResult>.Success(
            new BceaComplianceResult(violationsList, Array.Empty<string>()));
    }

    /// <summary>
    /// Checks leave balance compliance against BCEA leave entitlement minimums.
    /// </summary>
    /// <param name="annualLeaveBalance">Current annual leave balance in working days.</param>
    /// <param name="employmentMonths">Number of complete months of employment.</param>
    /// <returns>Result containing compliance result with warnings if balance is low.</returns>
    public static Result<BceaComplianceResult> CheckLeaveCompliance(decimal annualLeaveBalance, int employmentMonths)
    {
        // VUL-025: Validate input
        if (employmentMonths < 0)
        {
            return Result<BceaComplianceResult>.Failure(
                ZenoHrErrorCode.ValidationFailed, "Employment months cannot be negative.");
        }

        var warnings = new List<string>();

        // CTL-BCEA-003: Pro-rated minimum = months × 1.25 days/month
        var proRatedMinimum = employmentMonths * MonthlyLeaveAccrualRate;

        if (annualLeaveBalance < proRatedMinimum)
        {
            warnings.Add(
                $"Annual leave balance ({annualLeaveBalance} days) is below the BCEA pro-rated minimum " +
                $"of {proRatedMinimum} days for {employmentMonths} month(s) of employment " +
                $"(rate: {MonthlyLeaveAccrualRate} days/month).");
        }

        return Result<BceaComplianceResult>.Success(
            new BceaComplianceResult(Array.Empty<string>(), warnings));
    }

    /// <summary>
    /// Runs all BCEA pre-payroll compliance checks.
    /// Payroll finalization is blocked if any violations exist (warnings are non-blocking).
    /// </summary>
    public static Result<BceaComplianceResult> ValidatePrePayroll(
        decimal weeklyHours,
        bool isOvertimeAgreed,
        decimal annualLeaveBalance,
        int employmentMonths)
    {
        var overtimeResult = CheckOvertimeCompliance(weeklyHours, isOvertimeAgreed);
        if (overtimeResult.IsFailure)
            return overtimeResult;

        var leaveResult = CheckLeaveCompliance(annualLeaveBalance, employmentMonths);
        if (leaveResult.IsFailure)
            return leaveResult;

        // Combine violations and warnings from both checks
        var allViolations = overtimeResult.Value.Violations
            .Concat(leaveResult.Value.Violations)
            .ToList();

        var allWarnings = overtimeResult.Value.Warnings
            .Concat(leaveResult.Value.Warnings)
            .ToList();

        return Result<BceaComplianceResult>.Success(
            new BceaComplianceResult(allViolations, allWarnings));
    }
}
