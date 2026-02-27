// REQ-HR-003: Typed adaptor over StatutoryRuleSet for the SARS_PAYE domain.
// CTL-SARS-001: All PAYE calculation inputs sourced from this adaptor — never hardcoded.
// PRD-16 Sections 1–3: PayeCalculationEngine depends on this type for brackets + rebates.

using ZenoHR.Domain.Common;

namespace ZenoHR.Module.Payroll.Calculation;

/// <summary>
/// Typed view over a <see cref="StatutoryRuleSet"/> with <c>RuleDomain == "SARS_PAYE"</c>.
/// Extracts tax brackets, rebates, and thresholds from the untyped
/// <see cref="StatutoryRuleSet.RuleData"/> dictionary.
/// <para>
/// Created once per payroll run via <see cref="From"/> and passed to
/// <see cref="PayeCalculationEngine"/>. All values are <see cref="decimal"/>.
/// </para>
/// CTL-SARS-001: The engine never reads raw <c>RuleData</c> directly — always via this adaptor.
/// </summary>
public sealed class SarsPayeRuleSet
{
    /// <summary>Progressive tax brackets, ordered lowest-to-highest.</summary>
    public IReadOnlyList<PayeTaxBracket> Brackets { get; }

    /// <summary>Primary rebate — applies to all taxpayers (ZAR).</summary>
    public decimal PrimaryRebate { get; }

    /// <summary>Secondary rebate — applies additionally if age ≥ 65 (ZAR).</summary>
    public decimal SecondaryRebate { get; }

    /// <summary>Tertiary rebate — applies additionally if age ≥ 75 (ZAR).</summary>
    public decimal TertiaryRebate { get; }

    /// <summary>Annual tax threshold for employees under 65 (ZAR). Income below this → zero PAYE.</summary>
    public decimal ThresholdBelow65 { get; }

    /// <summary>Annual tax threshold for employees aged 65–74 (ZAR).</summary>
    public decimal ThresholdAge65To74 { get; }

    /// <summary>Annual tax threshold for employees aged 75+ (ZAR).</summary>
    public decimal ThresholdAge75Plus { get; }

    /// <summary>Tax year label, e.g. "2026".</summary>
    public string TaxYear { get; }

    private SarsPayeRuleSet(
        IReadOnlyList<PayeTaxBracket> brackets,
        decimal primary, decimal secondary, decimal tertiary,
        decimal t65, decimal t65to74, decimal t75,
        string taxYear)
    {
        Brackets = brackets;
        PrimaryRebate = primary;
        SecondaryRebate = secondary;
        TertiaryRebate = tertiary;
        ThresholdBelow65 = t65;
        ThresholdAge65To74 = t65to74;
        ThresholdAge75Plus = t75;
        TaxYear = taxYear;
    }

    /// <summary>
    /// Constructs a typed rule set from a raw <see cref="StatutoryRuleSet"/>.
    /// Throws <see cref="InvalidOperationException"/> if the domain is wrong or required keys are absent.
    /// CTL-SARS-001: Called once during payroll run initialisation.
    /// </summary>
    public static SarsPayeRuleSet From(StatutoryRuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        if (ruleSet.RuleDomain != RuleDomains.SarsPaye)
            throw new InvalidOperationException(
                $"Expected RuleDomain '{RuleDomains.SarsPaye}' but got '{ruleSet.RuleDomain}'.");

        var data = ruleSet.RuleData;

        // ── Tax brackets ────────────────────────────────────────────────────
        var bracketList = StatutoryDataConverter.GetList(data, "tax_brackets");
        var brackets = bracketList
            .Cast<IDictionary<string, object?>>()
            .Select(b => new PayeTaxBracket
            {
                Min = StatutoryDataConverter.ToDecimal(b["min"]),
                Max = b.TryGetValue("max", out var maxVal) && maxVal is not null
                    ? StatutoryDataConverter.ToDecimal(maxVal)
                    : (decimal?)null,
                Rate = StatutoryDataConverter.ToDecimal(b["rate"]),
                BaseTax = StatutoryDataConverter.ToDecimal(b["base_tax"]),
            })
            .ToList()
            .AsReadOnly();

        // ── Rebates ─────────────────────────────────────────────────────────
        var rebates = StatutoryDataConverter.GetDict(data, "rebates");
        var primary   = StatutoryDataConverter.ToDecimal(rebates["primary"]);
        var secondary = StatutoryDataConverter.ToDecimal(rebates["secondary_age_65_plus"]);
        var tertiary  = StatutoryDataConverter.ToDecimal(rebates["tertiary_age_75_plus"]);

        // ── Thresholds ───────────────────────────────────────────────────────
        var thresholds = StatutoryDataConverter.GetDict(data, "tax_thresholds");
        var t65      = StatutoryDataConverter.ToDecimal(thresholds["below_age_65"]);
        var t65to74  = StatutoryDataConverter.ToDecimal(thresholds["age_65_to_74"]);
        var t75      = StatutoryDataConverter.ToDecimal(thresholds["age_75_and_over"]);

        return new SarsPayeRuleSet(brackets, primary, secondary, tertiary, t65, t65to74, t75,
            ruleSet.TaxYear);
    }

    /// <summary>
    /// Creates a rule set directly from typed values. Used in unit tests only.
    /// CTL-SARS-001: Production code must always use <see cref="From(StatutoryRuleSet)"/>.
    /// </summary>
    public static SarsPayeRuleSet CreateForTesting(
        IReadOnlyList<PayeTaxBracket> brackets,
        decimal primary, decimal secondary, decimal tertiary,
        decimal thresholdBelow65, decimal thresholdAge65To74, decimal thresholdAge75Plus,
        string taxYear = "2026") =>
        new(brackets, primary, secondary, tertiary,
            thresholdBelow65, thresholdAge65To74, thresholdAge75Plus, taxYear);
}
