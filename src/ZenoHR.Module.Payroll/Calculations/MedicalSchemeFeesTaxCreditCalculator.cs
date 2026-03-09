// REQ-HR-003, CTL-SARS-005: Medical Scheme Fees Tax Credit (MSFTC) calculator.
// Income Tax Act Section 6A — IRP5 code 4116.
// Non-refundable credit applied against PAYE liability after primary/secondary/tertiary rebates.
// Rates are NEVER hardcoded here — they come from StatutoryRuleSet (RuleDomains.SarsMsftc).

namespace ZenoHR.Module.Payroll.Calculations;

/// <summary>
/// Pure static calculator for the Medical Scheme Fees Tax Credit (MSFTC).
/// Implements Income Tax Act Section 6A as specified in PRD-16.
/// <para>
/// <strong>Rate source:</strong> All credit amounts (primary, dependant, additional) must be
/// loaded from <c>StatutoryRuleSet</c> (rule domain <c>SARS_MSFTC</c>) by the calling service.
/// Never hardcode R364 / R246 or any other monetary amount in this class.
/// </para>
/// <para>
/// <strong>Non-refundable:</strong> The credit reduces PAYE liability but cannot create a refund.
/// <see cref="ApplyCredit"/> floors the result at zero.
/// </para>
/// REQ-HR-003, CTL-SARS-005
/// </summary>
public static class MedicalSchemeFeesTaxCreditCalculator
{
    /// <summary>
    /// Calculates the total monthly MSFTC for an employee based on their registered dependants.
    /// </summary>
    /// <param name="dependantCount">
    /// Number of registered medical scheme dependants (excluding the principal member).
    /// 0 = principal only; 1 = principal + 1 dependant; 2 = principal + 2 dependants; etc.
    /// </param>
    /// <param name="primaryMonthlyCredit">
    /// Monthly credit for the principal member. Loaded from <c>StatutoryRuleSet</c>.
    /// </param>
    /// <param name="dependantMonthlyCredit">
    /// Monthly credit for the first dependant. Loaded from <c>StatutoryRuleSet</c>.
    /// </param>
    /// <param name="additionalMonthlyCredit">
    /// Monthly credit for each additional dependant beyond the first. Loaded from <c>StatutoryRuleSet</c>.
    /// </param>
    /// <returns>Total monthly MSFTC in ZAR (decimal).</returns>
    /// <remarks>
    /// PRD-16 MSFTC formula:
    /// credit = primary
    /// if dependants >= 1: credit += dependant_1_credit
    /// if dependants >= 2: credit += additional_credit × (dependants − 1)
    /// </remarks>
    public static decimal CalculateMonthly(
        int dependantCount,
        decimal primaryMonthlyCredit,
        decimal dependantMonthlyCredit,
        decimal additionalMonthlyCredit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dependantCount);
        if (primaryMonthlyCredit < 0)
            throw new ArgumentOutOfRangeException(nameof(primaryMonthlyCredit),
                "Primary monthly credit cannot be negative.");
        if (dependantMonthlyCredit < 0)
            throw new ArgumentOutOfRangeException(nameof(dependantMonthlyCredit),
                "Dependant monthly credit cannot be negative.");
        if (additionalMonthlyCredit < 0)
            throw new ArgumentOutOfRangeException(nameof(additionalMonthlyCredit),
                "Additional monthly credit cannot be negative.");

        var credit = primaryMonthlyCredit;
        if (dependantCount >= 1) credit += dependantMonthlyCredit;
        if (dependantCount >= 2) credit += additionalMonthlyCredit * (dependantCount - 1);
        return credit;
    }

    /// <summary>
    /// Applies the monthly MSFTC against a PAYE liability.
    /// Non-refundable: result is floored at zero (Section 6A — credit cannot produce a refund).
    /// </summary>
    /// <param name="payeLiabilityBeforeMsftc">PAYE liability after rebates but before MSFTC. Must be ≥ 0.</param>
    /// <param name="monthlyMsftc">Monthly MSFTC calculated by <see cref="CalculateMonthly"/>. Must be ≥ 0.</param>
    /// <returns>Reduced PAYE liability, floored at zero.</returns>
    public static decimal ApplyCredit(decimal payeLiabilityBeforeMsftc, decimal monthlyMsftc)
    {
        if (payeLiabilityBeforeMsftc < 0)
            throw new ArgumentOutOfRangeException(nameof(payeLiabilityBeforeMsftc),
                "PAYE liability before MSFTC cannot be negative.");
        if (monthlyMsftc < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyMsftc),
                "Monthly MSFTC cannot be negative.");

        return Math.Max(0m, payeLiabilityBeforeMsftc - monthlyMsftc);
    }

    /// <summary>
    /// Converts a monthly MSFTC to an annualised credit for use in annual equivalent calculations.
    /// PRD-16: annualised credit = monthly credit × periods per year (12 for monthly, 52 for weekly).
    /// </summary>
    /// <param name="monthlyCredit">The monthly credit amount.</param>
    /// <param name="periodsPerYear">Number of pay periods per year (12 or 52).</param>
    /// <returns>Annualised MSFTC.</returns>
    public static decimal AnnualiseCredit(decimal monthlyCredit, int periodsPerYear)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(periodsPerYear);
        return monthlyCredit * periodsPerYear;
    }
}
