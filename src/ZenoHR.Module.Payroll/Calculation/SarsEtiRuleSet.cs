// REQ-HR-003: Typed adaptor over StatutoryRuleSet for the SARS_ETI domain.
// CTL-SARS-003: ETI eligibility criteria and tier rates sourced from this adaptor.
// PRD-16 Section 5: ETI tier determination and rate calculation.

using ZenoHR.Domain.Common;

namespace ZenoHR.Module.Payroll.Calculation;

/// <summary>
/// A single ETI tier rate band extracted from the seed data.
/// Both Tier1 and Tier2 have three bands each (below minimum wage, flat, tapering).
/// </summary>
public sealed record EtiRateBand
{
    public decimal MinRemuneration { get; init; }
    public decimal MaxRemuneration { get; init; }
    /// <summary>
    /// The formula type for this band.
    /// "percentage" = rate × remuneration;
    /// "fixed" = fixed flat amount;
    /// "taper" = flatAmount − (taperRate × (remuneration − taperFloor)).
    /// </summary>
    public string FormulaType { get; init; } = "fixed";
    public decimal Rate { get; init; }          // for "percentage" bands
    public decimal FlatAmount { get; init; }     // for "fixed" and "taper" bands
    public decimal TaperRate { get; init; }      // for "taper" bands
    public decimal TaperFloor { get; init; }     // for "taper" bands (R5,500)
}

/// <summary>
/// Typed view over a <see cref="StatutoryRuleSet"/> with <c>RuleDomain == "SARS_ETI"</c>.
/// Extracts eligibility criteria and rate bands for the Employment Tax Incentive.
/// CTL-SARS-003: EtiCalculationEngine never reads RuleData directly — always via this adaptor.
/// </summary>
public sealed class SarsEtiRuleSet
{
    /// <summary>Minimum employee age to qualify for ETI (inclusive). Default 18.</summary>
    public int EligibilityAgeMin { get; }

    /// <summary>Maximum employee age to qualify for ETI (inclusive). Default 29.</summary>
    public int EligibilityAgeMax { get; }

    /// <summary>Minimum monthly remuneration required to claim ETI (e.g., R2,500).</summary>
    public decimal MinimumMonthlyWage { get; }

    /// <summary>Maximum monthly remuneration for ETI eligibility (e.g., R7,500).</summary>
    public decimal MaximumMonthlyRemuneration { get; }

    /// <summary>Rate bands for the first 12 qualifying months (higher rates).</summary>
    public IReadOnlyList<EtiRateBand> Tier1Bands { get; }

    /// <summary>Rate bands for months 13–24 (reduced rates).</summary>
    public IReadOnlyList<EtiRateBand> Tier2Bands { get; }

    /// <summary>Standard hours used for proration (160 hours/month).</summary>
    public int StandardHoursPerMonth { get; }

    private SarsEtiRuleSet(
        int ageMin, int ageMax,
        decimal minWage, decimal maxRemuneration,
        IReadOnlyList<EtiRateBand> tier1, IReadOnlyList<EtiRateBand> tier2,
        int standardHours)
    {
        EligibilityAgeMin = ageMin;
        EligibilityAgeMax = ageMax;
        MinimumMonthlyWage = minWage;
        MaximumMonthlyRemuneration = maxRemuneration;
        Tier1Bands = tier1;
        Tier2Bands = tier2;
        StandardHoursPerMonth = standardHours;
    }

    /// <summary>
    /// Constructs a typed ETI rule set from a raw <see cref="StatutoryRuleSet"/>.
    /// CTL-SARS-003
    /// </summary>
    public static SarsEtiRuleSet From(StatutoryRuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        if (ruleSet.RuleDomain != RuleDomains.SarsEti)
            throw new InvalidOperationException(
                $"Expected RuleDomain '{RuleDomains.SarsEti}' but got '{ruleSet.RuleDomain}'.");

        var data = ruleSet.RuleData;

        var elig = StatutoryDataConverter.GetDict(data, "eligibility");
        var ageMin   = (int)StatutoryDataConverter.ToDecimal(elig["employee_age_min"]);
        var ageMax   = (int)StatutoryDataConverter.ToDecimal(elig["employee_age_max"]);
        var minWage  = StatutoryDataConverter.ToDecimal(elig["minimum_monthly_wage"]);
        var maxRem   = StatutoryDataConverter.ToDecimal(elig["maximum_monthly_remuneration"]);

        var hours    = StatutoryDataConverter.GetDict(data, "hours_proration");
        var stdHours = (int)StatutoryDataConverter.ToDecimal(hours["standard_hours"]);

        var tier1 = ParseTierBands(StatutoryDataConverter.GetDict(data, "first_12_months"));
        var tier2 = ParseTierBands(StatutoryDataConverter.GetDict(data, "second_12_months"));

        return new SarsEtiRuleSet(ageMin, ageMax, minWage, maxRem, tier1, tier2, stdHours);
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<EtiRateBand> ParseTierBands(IDictionary<string, object?> tierData)
    {
        var tiersRaw = StatutoryDataConverter.GetList(
            (IReadOnlyDictionary<string, object?>)tierData, "tiers");

        return tiersRaw
            .Cast<IDictionary<string, object?>>()
            .Select(t =>
            {
                var formula = (string)t["formula"]!;
                var minRem  = StatutoryDataConverter.ToDecimal(t["min_remuneration"]);
                var maxRem  = StatutoryDataConverter.ToDecimal(t["max_remuneration"]);

                // Determine the band type from the formula string pattern:
                // "0.60 * ..." → percentage
                // "1500.00"    → fixed
                // "1500.00 - (0.75 * ...)" → taper
                if (formula.Contains("- ("))
                {
                    // Taper: "1500.00 - (0.75 * (monthly_remuneration - 5500.00))"
                    var flatAmount = decimal.Parse(
                        formula[..formula.IndexOf(" - (", StringComparison.Ordinal)].Trim(),
                        System.Globalization.CultureInfo.InvariantCulture);
                    // Extract taper rate and floor from formula string
                    var taperMatch = System.Text.RegularExpressions.Regex.Match(
                        formula, @"\((\d+\.\d+) \* \(monthly_remuneration - (\d+\.\d+)\)\)");
                    var taperRate  = decimal.Parse(taperMatch.Groups[1].Value,
                        System.Globalization.CultureInfo.InvariantCulture);
                    var taperFloor = decimal.Parse(taperMatch.Groups[2].Value,
                        System.Globalization.CultureInfo.InvariantCulture);
                    return new EtiRateBand { MinRemuneration = minRem, MaxRemuneration = maxRem,
                        FormulaType = "taper", FlatAmount = flatAmount,
                        TaperRate = taperRate, TaperFloor = taperFloor };
                }
                else if (formula.Contains("* monthly_remuneration"))
                {
                    // Percentage: "0.60 * monthly_remuneration"
                    var rate = decimal.Parse(formula.Split('*')[0].Trim(),
                        System.Globalization.CultureInfo.InvariantCulture);
                    return new EtiRateBand { MinRemuneration = minRem, MaxRemuneration = maxRem,
                        FormulaType = "percentage", Rate = rate };
                }
                else
                {
                    // Fixed flat amount: "1500.00"
                    var flat = decimal.Parse(formula.Trim(),
                        System.Globalization.CultureInfo.InvariantCulture);
                    return new EtiRateBand { MinRemuneration = minRem, MaxRemuneration = maxRem,
                        FormulaType = "fixed", FlatAmount = flat };
                }
            })
            .ToList()
            .AsReadOnly();
    }

    /// <summary>Creates a rule set from typed values. Used in unit tests only.</summary>
    public static SarsEtiRuleSet CreateForTesting(
        IReadOnlyList<EtiRateBand> tier1Bands,
        IReadOnlyList<EtiRateBand> tier2Bands,
        int ageMin = 18, int ageMax = 29,
        decimal minWage = 2500m, decimal maxRemuneration = 7500m,
        int standardHours = 160) =>
        new(ageMin, ageMax, minWage, maxRemuneration, tier1Bands, tier2Bands, standardHours);
}
