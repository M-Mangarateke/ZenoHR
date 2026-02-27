// REQ-HR-003: PAYE calculation engine — SARS annual equivalent method.
// CTL-SARS-001: All rates, brackets, and rebates read from SarsPayeRuleSet (StatutoryRuleSet).
// PRD-16 Sections 1 (Monthly), 2 (Mid-Period Joiner), 3 (Weekly).

using ZenoHR.Domain.Common;

namespace ZenoHR.Module.Payroll.Calculation;

/// <summary>
/// Pure static PAYE calculation engine for South African individual income tax.
/// Implements the SARS "annual equivalent" method as specified in PRD-16.
/// <para>
/// All methods are <strong>pure functions</strong> — no I/O, no side effects, no Firestore access.
/// The calling service loads the <see cref="SarsPayeRuleSet"/> from Firestore and passes it in.
/// </para>
/// <para>
/// <strong>Rounding contract (PRD-16 Appendix A):</strong><br/>
/// Annual PAYE → nearest rand (<see cref="MoneyZAR.RoundToRand"/>).<br/>
/// Period PAYE → nearest cent (<see cref="MoneyZAR.RoundToCent"/>).<br/>
/// Both use <see cref="MidpointRounding.AwayFromZero"/>.
/// </para>
/// REQ-HR-003, CTL-SARS-001
/// </summary>
public static class PayeCalculationEngine
{
    // ── Public entry points ────────────────────────────────────────────────

    /// <summary>
    /// Calculates monthly PAYE using the SARS annual equivalent method.
    /// PRD-16 Section 1: annualise (×12) → brackets → rebates → floor at 0 → round rand →
    /// de-annualise (÷12) → round cent.
    /// </summary>
    /// <param name="monthlyTaxableIncome">
    /// Employee's monthly taxable remuneration. For monthly employees this is the full
    /// contractual salary (pension RA deduction already subtracted if applicable).
    /// </param>
    /// <param name="age">Employee's age at the last day of the payroll period.</param>
    /// <param name="ruleSet">Typed PAYE rule set for the applicable tax year.</param>
    /// <returns>Monthly PAYE to deduct, rounded to the nearest cent.</returns>
    public static MoneyZAR CalculateMonthlyPAYE(
        MoneyZAR monthlyTaxableIncome, int age, SarsPayeRuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        if (age < 0) throw new ArgumentOutOfRangeException(nameof(age), "Age cannot be negative.");

        // Step 1: Annualise
        var annualIncome = monthlyTaxableIncome * 12m;

        // Steps 2–5: brackets → rebates → floor → round to rand
        var annualTax = CalculateAnnualTax(annualIncome, age, ruleSet);

        // Step 6–7: de-annualise ÷12, round to cent
        return (annualTax / 12m).RoundToCent();
    }

    /// <summary>
    /// Calculates weekly PAYE using the SARS annual equivalent method.
    /// PRD-16 Section 3: ×52 to annualise, ÷52 to de-annualise.
    /// February, leap years, and tax year changes do NOT affect the ÷52/×52 formula.
    /// </summary>
    public static MoneyZAR CalculateWeeklyPAYE(
        MoneyZAR weeklyTaxableIncome, int age, SarsPayeRuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        if (age < 0) throw new ArgumentOutOfRangeException(nameof(age), "Age cannot be negative.");

        // Step 1: Annualise using ×52 (not actual weeks in tax year)
        var annualIncome = weeklyTaxableIncome * 52m;

        // Steps 2–5: brackets → rebates → floor → round
        var annualTax = CalculateAnnualTax(annualIncome, age, ruleSet);

        // Step 6–7: de-annualise ÷52, round to cent
        return (annualTax / 52m).RoundToCent();
    }

    /// <summary>
    /// Calculates PAYE for a mid-period joiner (employee starts partway through the month).
    /// PRD-16 Section 2: PAYE annualisation uses the <strong>full monthly package</strong>
    /// (not the pro-rated salary). Only the resulting monthly PAYE figure is pro-rated.
    /// </summary>
    /// <param name="monthlyPackage">Full contractual monthly salary — never pro-rated.</param>
    /// <param name="daysWorked">Calendar days from join date to month-end (inclusive).</param>
    /// <param name="daysInMonth">Total calendar days in the month.</param>
    /// <returns>Pro-rated monthly PAYE, rounded to the nearest cent.</returns>
    public static MoneyZAR CalculateJoinerPAYE(
        MoneyZAR monthlyPackage, int daysWorked, int daysInMonth, int age, SarsPayeRuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        if (daysInMonth <= 0)
            throw new ArgumentOutOfRangeException(nameof(daysInMonth), "daysInMonth must be positive.");
        if (daysWorked < 0 || daysWorked > daysInMonth)
            throw new ArgumentOutOfRangeException(nameof(daysWorked),
                $"daysWorked ({daysWorked}) must be in [0, daysInMonth={daysInMonth}].");
        if (age < 0)
            throw new ArgumentOutOfRangeException(nameof(age), "Age cannot be negative.");

        // Step 1: PAYE on full monthly package (SARS annualisation — never on partial salary)
        var fullMonthlyPaye = CalculateMonthlyPAYE(monthlyPackage, age, ruleSet);

        // Step 2: Pro-rate the PAYE result only
        return (fullMonthlyPaye * ((decimal)daysWorked / (decimal)daysInMonth)).RoundToCent();
    }

    /// <summary>
    /// Returns the annual tax threshold applicable for the given age.
    /// Income at or below this threshold results in zero PAYE.
    /// PRD-16 Section 1: below_65 / 65–74 / 75+.
    /// </summary>
    public static MoneyZAR GetAnnualThreshold(int age, SarsPayeRuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        return new MoneyZAR(
            age >= 75 ? ruleSet.ThresholdAge75Plus :
            age >= 65 ? ruleSet.ThresholdAge65To74 :
                        ruleSet.ThresholdBelow65);
    }

    // ── Internal helpers (internal for unit-test access) ──────────────────

    /// <summary>
    /// Core calculation: apply brackets → subtract rebates → floor at zero → round to rand.
    /// Shared by both Monthly and Weekly paths (PRD-16 Section 1 Steps 2–5).
    /// </summary>
    internal static MoneyZAR CalculateAnnualTax(MoneyZAR annualIncome, int age, SarsPayeRuleSet ruleSet)
    {
        // Step 2: Apply progressive tax brackets
        var grossTax = ApplyBrackets(annualIncome, ruleSet.Brackets);

        // Step 3: Subtract rebates
        var tax = grossTax - GetRebate(age, ruleSet);

        // Step 4: Floor at zero (never negative)
        var floored = tax.FloorAtZero();

        // Step 5: Round to nearest rand (AwayFromZero)
        return floored.RoundToRand();
    }

    /// <summary>
    /// Applies the progressive bracket table to <paramref name="annualIncome"/>.
    /// PRD-16 Section 1 Step 2 formula: <c>tax = base_tax + rate × (income − (min − 1))</c>
    /// </summary>
    internal static MoneyZAR ApplyBrackets(MoneyZAR annualIncome, IReadOnlyList<PayeTaxBracket> brackets)
    {
        if (annualIncome.IsZeroOrNegative) return MoneyZAR.Zero;

        foreach (var bracket in brackets)
        {
            if (bracket.Contains(annualIncome.Amount))
            {
                // Income above the bracket floor × marginal rate + cumulative base
                var excessAboveFloor = annualIncome.Amount - (bracket.Min - 1m);
                var tax = bracket.BaseTax + bracket.Rate * excessAboveFloor;
                return new MoneyZAR(tax);
            }
        }

        // The last bracket must have Max == null and catch all remaining income.
        // If we reach here the bracket table is malformed.
        throw new InvalidOperationException(
            $"Annual income {annualIncome} did not match any tax bracket. " +
            $"Verify that the last bracket in the SARS_PAYE StatutoryRuleSet has Max = null.");
    }

    /// <summary>
    /// Returns total applicable rebate: primary always; +secondary if age≥65; +tertiary if age≥75.
    /// PRD-16 Section 1 Step 3.
    /// </summary>
    internal static MoneyZAR GetRebate(int age, SarsPayeRuleSet ruleSet)
    {
        var rebate = ruleSet.PrimaryRebate;
        if (age >= 65) rebate += ruleSet.SecondaryRebate;
        if (age >= 75) rebate += ruleSet.TertiaryRebate;
        return new MoneyZAR(rebate);
    }
}
