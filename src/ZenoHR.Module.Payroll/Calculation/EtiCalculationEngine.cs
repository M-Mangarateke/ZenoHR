// REQ-HR-003: Employment Tax Incentive calculation engine.
// CTL-SARS-003: ETI eligibility, tier determination, and rate calculation per PRD-16 Section 5.
// PRD-16 Section 5: 12-month qualifying period per employment relationship (not per person).

using ZenoHR.Domain.Common;

namespace ZenoHR.Module.Payroll.Calculation;

/// <summary>
/// Pure static Employment Tax Incentive (ETI) calculation engine.
/// <para>
/// ETI is a <strong>employer-side reduction</strong> to the employer's EMP201 PAYE remittance.
/// It is NOT deducted from the employee's salary. The employee's net pay is unaffected.
/// </para>
/// <para>
/// <strong>Eligibility (PRD-16 Section 5 / CTL-SARS-003):</strong>
/// <list type="bullet">
///   <item>Employee age 18–29 at the calculation date</item>
///   <item>Monthly remuneration ≥ R2,500 (minimum wage) and ≤ R7,500</item>
///   <item>Employment relationship &lt; 24 completed calendar months</item>
///   <item>Employer must not have displaced an existing employee</item>
/// </list>
/// </para>
/// <para>
/// <strong>Tier determination (PRD-16 Section 5):</strong><br/>
/// Months ≤ 12 → Tier1 (higher rates). Months 13–24 → Tier2 (reduced rates).
/// After 24 months → Ineligible. The clock resets on termination + rehire.
/// </para>
/// REQ-HR-003, CTL-SARS-003
/// </summary>
public static class EtiCalculationEngine
{
    // ── Tier determination ─────────────────────────────────────────────────

    /// <summary>
    /// Determines the ETI qualifying tier based on the number of completed calendar months
    /// since the employment start date.
    /// PRD-16 Section 5 / GetETITier pseudocode.
    /// </summary>
    /// <param name="employmentStartDate">
    /// The <c>employment_contracts.etiquette_start_date</c> field.
    /// This resets on termination + rehire (new employment relationship = new clock).
    /// </param>
    /// <param name="calculationDate">The last day of the payroll period being calculated.</param>
    public static EtiTier GetTier(DateOnly employmentStartDate, DateOnly calculationDate)
    {
        var monthsEmployed = CalendarMonthsBetween(employmentStartDate, calculationDate);
        return monthsEmployed switch
        {
            <= 0 => EtiTier.Ineligible,
            <= 12 => EtiTier.Tier1,
            <= 24 => EtiTier.Tier2,
            _ => EtiTier.Ineligible,
        };
    }

    /// <summary>
    /// Counts the number of complete calendar months from <paramref name="startDate"/> to
    /// <paramref name="endDate"/> (exclusive of end date).
    /// PRD-16 Section 5: 2025-01-15 → 2025-03-15 = 2; 2025-01-15 → 2025-03-14 = 1.
    /// </summary>
    public static int CalendarMonthsBetween(DateOnly startDate, DateOnly endDate)
    {
        if (endDate <= startDate) return 0;

        var months = (endDate.Year - startDate.Year) * 12
                   + (endDate.Month - startDate.Month);

        // If the end day is before the start day, the last month is not yet complete
        if (endDate.Day < startDate.Day)
            months--;

        return Math.Max(0, months);
    }

    // ── Eligibility check ─────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the employee is eligible for ETI in this payroll period.
    /// PRD-16 Section 5 / CTL-SARS-003 eligibility criteria.
    /// </summary>
    /// <param name="ageAtCalculationDate">Employee's age on the last day of the period.</param>
    /// <param name="monthlyRemuneration">Employee's gross monthly remuneration.</param>
    /// <param name="tier">Pre-determined ETI tier (from <see cref="GetTier"/>).</param>
    /// <param name="rules">Typed ETI rule set.</param>
    public static bool IsEligible(
        int ageAtCalculationDate, MoneyZAR monthlyRemuneration, EtiTier tier, SarsEtiRuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (tier == EtiTier.Ineligible) return false;
        if (ageAtCalculationDate < rules.EligibilityAgeMin) return false;
        if (ageAtCalculationDate > rules.EligibilityAgeMax) return false;
        if (monthlyRemuneration < new MoneyZAR(rules.MinimumMonthlyWage)) return false;
        if (monthlyRemuneration > new MoneyZAR(rules.MaximumMonthlyRemuneration)) return false;
        return true;
    }

    // ── ETI amount calculation ────────────────────────────────────────────

    /// <summary>
    /// Calculates the monthly ETI amount for an eligible employee.
    /// Returns <see cref="MoneyZAR.Zero"/> if the employee is not eligible.
    /// PRD-16 Section 5 rate bands.
    /// </summary>
    /// <param name="monthlyRemuneration">Employee's gross monthly remuneration.</param>
    /// <param name="tier">ETI tier determined by <see cref="GetTier"/>.</param>
    /// <param name="actualHoursWorked">
    /// Actual hours worked. If &lt; <see cref="SarsEtiRuleSet.StandardHoursPerMonth"/>,
    /// the ETI amount is pro-rated (hours_proration rule from seed data).
    /// </param>
    /// <param name="rules">Typed ETI rule set.</param>
    public static MoneyZAR CalculateMonthlyEti(
        MoneyZAR monthlyRemuneration, EtiTier tier, int actualHoursWorked, SarsEtiRuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (tier == EtiTier.Ineligible) return MoneyZAR.Zero;
        if (monthlyRemuneration > new MoneyZAR(rules.MaximumMonthlyRemuneration)) return MoneyZAR.Zero;
        if (monthlyRemuneration < new MoneyZAR(rules.MinimumMonthlyWage)) return MoneyZAR.Zero;

        var bands = tier == EtiTier.Tier1 ? rules.Tier1Bands : rules.Tier2Bands;

        // If hours < standard, gross up remuneration for tier lookup, then prorate result
        decimal remunerationForTier;
        decimal prorationFactor;
        if (actualHoursWorked < rules.StandardHoursPerMonth && actualHoursWorked > 0)
        {
            remunerationForTier = monthlyRemuneration.Amount
                * rules.StandardHoursPerMonth / actualHoursWorked;
            prorationFactor = (decimal)actualHoursWorked / rules.StandardHoursPerMonth;
        }
        else
        {
            remunerationForTier = monthlyRemuneration.Amount;
            prorationFactor = 1m;
        }

        var rawEti = ApplyEtiBands(remunerationForTier, bands);
        var prorated = rawEti * prorationFactor;

        return new MoneyZAR(prorated).RoundToCent();
    }

    // ── Internal helpers ──────────────────────────────────────────────────

    internal static decimal ApplyEtiBands(decimal remuneration, IReadOnlyList<EtiRateBand> bands)
    {
        foreach (var band in bands)
        {
            if (remuneration >= band.MinRemuneration && remuneration <= band.MaxRemuneration)
            {
                return band.FormulaType switch
                {
                    "percentage" => band.Rate * remuneration,
                    "fixed" => band.FlatAmount,
                    "taper" => band.FlatAmount - band.TaperRate * (remuneration - band.TaperFloor),
                    _ => throw new InvalidOperationException(
                        $"Unknown ETI band FormulaType '{band.FormulaType}'."),
                };
            }
        }
        return 0m; // Remuneration outside all bands → no ETI
    }
}
