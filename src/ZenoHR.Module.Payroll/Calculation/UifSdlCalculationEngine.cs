// REQ-HR-003: UIF and SDL calculation engine.
// CTL-SARS-002: UIF = min(gross × 0.01, R177.12). SDL = gross × 0.01 (employer only, exempt if < R500k/yr).
// PRD-16 Section 6 (floor interaction), Section 8 (UIF ceiling), Section 10 (EMP201 SDL field).

using ZenoHR.Domain.Common;

namespace ZenoHR.Module.Payroll.Calculation;

/// <summary>
/// Pure static engine for Unemployment Insurance Fund (UIF) and Skills Development Levy (SDL).
/// <para>
/// <strong>UIF (PRD-16 Section 8)</strong>: Calculated on gross remuneration before PAYE.
/// UIF is NOT deductible for PAYE purposes. Ceiling applies to both employee and employer sides.
/// </para>
/// <para>
/// <strong>SDL (PRD-16 Section 6)</strong>: Employer-only levy. Cannot be deducted from employee salary.
/// Exempt if employer's total annual leviable remuneration is below <see cref="SarsUifSdlRuleSet.SdlExemptionThresholdAnnual"/>.
/// </para>
/// REQ-HR-003, CTL-SARS-002
/// </summary>
public static class UifSdlCalculationEngine
{
    // ── UIF ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates the employee's monthly UIF contribution.
    /// PRD-16 Section 8: <c>uif_employee = Min(gross × 0.01, R177.12)</c>
    /// </summary>
    /// <param name="grossMonthlyPay">Employee's gross monthly remuneration before any deductions.</param>
    /// <param name="rules">Typed UIF/SDL rule set for the applicable tax year.</param>
    /// <returns>Employee UIF deduction, rounded to the nearest cent.</returns>
    public static MoneyZAR CalculateUifEmployee(MoneyZAR grossMonthlyPay, SarsUifSdlRuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var raw = grossMonthlyPay * rules.UifEmployeeRate;
        var capped = MoneyZAR.Min(raw, new MoneyZAR(rules.MaxEmployeeMonthly));
        return capped.RoundToCent();
    }

    /// <summary>
    /// Calculates the employer's monthly UIF contribution (not deducted from employee).
    /// PRD-16 Section 8: <c>uif_employer = Min(gross × 0.01, R177.12)</c>
    /// </summary>
    public static MoneyZAR CalculateUifEmployer(MoneyZAR grossMonthlyPay, SarsUifSdlRuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var raw = grossMonthlyPay * rules.UifEmployerRate;
        var capped = MoneyZAR.Min(raw, new MoneyZAR(rules.MaxEmployerMonthly));
        return capped.RoundToCent();
    }

    /// <summary>
    /// Calculates weekly UIF by pro-rating the monthly ceiling across 4.333 weeks.
    /// PRD-16 Section 8 note: weekly ceiling = R17,712 / 4.333 ≈ R4,087.74.
    /// </summary>
    public static MoneyZAR CalculateUifEmployeeWeekly(MoneyZAR grossWeeklyPay, SarsUifSdlRuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        // Weekly ceiling = monthly ceiling / (52/12) = monthly ceiling × 12/52
        var weeklyCeiling = rules.UifMonthlyCeiling * 12m / 52m;
        var cappedPay = MoneyZAR.Min(grossWeeklyPay, new MoneyZAR(weeklyCeiling));
        return (cappedPay * rules.UifEmployeeRate).RoundToCent();
    }

    public static MoneyZAR CalculateUifEmployerWeekly(MoneyZAR grossWeeklyPay, SarsUifSdlRuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var weeklyCeiling = rules.UifMonthlyCeiling * 12m / 52m;
        var cappedPay = MoneyZAR.Min(grossWeeklyPay, new MoneyZAR(weeklyCeiling));
        return (cappedPay * rules.UifEmployerRate).RoundToCent();
    }

    // ── SDL ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates the employer's monthly SDL contribution.
    /// PRD-16 Section 6: <c>sdl = gross × 0.01</c>. Returns zero if employer is SDL-exempt.
    /// SDL is employer-only — never deducted from employee net pay.
    /// </summary>
    /// <param name="grossMonthlyPay">Employee's gross monthly remuneration.</param>
    /// <param name="rules">Typed UIF/SDL rule set.</param>
    /// <param name="isEmployerSdlExempt">
    /// True if the employer's annual leviable payroll is below
    /// <see cref="SarsUifSdlRuleSet.SdlExemptionThresholdAnnual"/>.
    /// The orchestration service determines exemption status before calling this method.
    /// </param>
    public static MoneyZAR CalculateSdl(
        MoneyZAR grossMonthlyPay, SarsUifSdlRuleSet rules, bool isEmployerSdlExempt)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (isEmployerSdlExempt) return MoneyZAR.Zero;
        return (grossMonthlyPay * rules.SdlRate).RoundToCent();
    }
}
