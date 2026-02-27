// REQ-HR-003: PAYE tax bracket value object.
// CTL-SARS-001: All bracket values sourced from StatutoryRuleSet — never hardcoded.
// PRD-16 Section 1 (ApplyBrackets): tax = base_tax + rate × (income − (min − 1)).

namespace ZenoHR.Module.Payroll.Calculation;

/// <summary>
/// Immutable value object representing a single SARS progressive income tax bracket.
/// Loaded from <c>StatutoryRuleSet.RuleData["tax_brackets"]</c> via <see cref="SarsPayeRuleSet"/>.
/// All monetary fields are in annual ZAR.
/// CTL-SARS-001: Never construct with hardcoded values — always from seed data via SarsPayeRuleSet.
/// </summary>
public sealed record PayeTaxBracket
{
    /// <summary>
    /// Lower bound of this bracket (annual income, ZAR, inclusive).
    /// For the first bracket this is 1; for subsequent brackets it is the previous max + 1.
    /// </summary>
    public decimal Min { get; init; }

    /// <summary>
    /// Upper bound of this bracket (annual income, ZAR, inclusive).
    /// Null for the highest bracket (no ceiling — applies to all income above <see cref="Min"/>).
    /// </summary>
    public decimal? Max { get; init; }

    /// <summary>
    /// Marginal rate for income within this bracket (e.g., 0.18m = 18%).
    /// </summary>
    public decimal Rate { get; init; }

    /// <summary>
    /// Cumulative tax on income up to the previous bracket's upper bound (ZAR).
    /// For the first bracket this is 0.
    /// PRD-16: tax = BaseTax + Rate × (annualIncome − (Min − 1))
    /// </summary>
    public decimal BaseTax { get; init; }

    /// <summary>Returns true if <paramref name="annualIncome"/> falls within this bracket.</summary>
    public bool Contains(decimal annualIncome) =>
        annualIncome >= Min && (Max is null || annualIncome <= Max.Value);
}
