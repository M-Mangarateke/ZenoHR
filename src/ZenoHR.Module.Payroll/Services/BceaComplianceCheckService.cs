// VUL-024, VUL-025: BCEA pre-payroll compliance checks.
// CTL-BCEA-001: Ordinary hours limits (45/week max).
// CTL-BCEA-003: Leave entitlement minimums (15 days/year, pro-rated 1.25 days/month).
// Critical Rule #1: All statutory values injected via BceaComplianceOptions — never hardcoded.

using ZenoHR.Domain.Errors;
using ZenoHR.Module.Payroll.Models;

namespace ZenoHR.Module.Payroll.Services;

/// <summary>
/// Validates BCEA working-time and leave compliance before payroll finalization.
/// Overtime violations block payroll; leave balance warnings do not.
/// Statutory limits are injected via <see cref="BceaComplianceOptions"/> (sourced from StatutoryRuleSet).
/// </summary>
public sealed class BceaComplianceCheckService
{
    private readonly BceaComplianceOptions _options;

    /// <summary>
    /// Creates a new instance with the specified BCEA compliance options.
    /// </summary>
    /// <param name="options">BCEA statutory limits. Must not be null.</param>
    public BceaComplianceCheckService(BceaComplianceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>
    /// Checks overtime compliance against BCEA working-time limits.
    /// </summary>
    /// <param name="weeklyHours">Total hours worked in the week (ordinary + overtime).</param>
    /// <param name="isOvertimeAgreed">Whether a written overtime agreement exists.</param>
    /// <returns>Result containing compliance result, or failure on invalid input.</returns>
    public Result<BceaComplianceResult> CheckOvertimeCompliance(decimal weeklyHours, bool isOvertimeAgreed)
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
            // Without overtime agreement, maximum is ordinary hours only
            if (weeklyHours > _options.MaxOrdinaryHoursPerWeek)
            {
                violationsList.Add(
                    $"No overtime agreement in place. Employee worked {weeklyHours}h but maximum without agreement is {_options.MaxOrdinaryHoursPerWeek}h/week. " +
                    "A written overtime agreement is required under BCEA Section 10.");
            }
        }
        else
        {
            // With overtime agreement, ordinary limit still applies + overtime capped
            if (weeklyHours > _options.MaxTotalHoursWithAgreement)
            {
                violationsList.Add(
                    $"Total weekly hours ({weeklyHours}h) exceed BCEA maximum of {_options.MaxTotalHoursWithAgreement}h/week " +
                    $"(ordinary {_options.MaxOrdinaryHoursPerWeek}h + overtime {_options.MaxOvertimeHoursPerWeek}h).");
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
    public Result<BceaComplianceResult> CheckLeaveCompliance(decimal annualLeaveBalance, int employmentMonths)
    {
        // VUL-025: Validate input
        if (employmentMonths < 0)
        {
            return Result<BceaComplianceResult>.Failure(
                ZenoHrErrorCode.ValidationFailed, "Employment months cannot be negative.");
        }

        var warnings = new List<string>();

        // CTL-BCEA-003: Pro-rated minimum = months x monthly accrual rate
        var proRatedMinimum = employmentMonths * _options.MonthlyLeaveAccrualRate;

        if (annualLeaveBalance < proRatedMinimum)
        {
            warnings.Add(
                $"Annual leave balance ({annualLeaveBalance} days) is below the BCEA pro-rated minimum " +
                $"of {proRatedMinimum} days for {employmentMonths} month(s) of employment " +
                $"(rate: {_options.MonthlyLeaveAccrualRate} days/month).");
        }

        return Result<BceaComplianceResult>.Success(
            new BceaComplianceResult(Array.Empty<string>(), warnings));
    }

    /// <summary>
    /// Runs all BCEA pre-payroll compliance checks.
    /// Payroll finalization is blocked if any violations exist (warnings are non-blocking).
    /// </summary>
    public Result<BceaComplianceResult> ValidatePrePayroll(
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
