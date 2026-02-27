// REQ-HR-003: Typed adaptor over StatutoryRuleSet for the SARS_UIF_SDL domain.
// CTL-SARS-002: UIF rates, ceiling, and SDL rate sourced from this adaptor.
// PRD-16 Sections 6 and 8: UIF ceiling interaction and floor-zero interaction.

using ZenoHR.Domain.Common;

namespace ZenoHR.Module.Payroll.Calculation;

/// <summary>
/// Typed view over a <see cref="StatutoryRuleSet"/> with <c>RuleDomain == "SARS_UIF_SDL"</c>.
/// Extracts UIF and SDL rates, ceilings, and exemption thresholds from the untyped
/// <see cref="StatutoryRuleSet.RuleData"/>.
/// CTL-SARS-002: UifSdlCalculationEngine never reads RuleData directly — always via this adaptor.
/// </summary>
public sealed class SarsUifSdlRuleSet
{
    /// <summary>Employee UIF contribution rate (e.g., 0.01 for 1%).</summary>
    public decimal UifEmployeeRate { get; }

    /// <summary>Employer UIF contribution rate (e.g., 0.01 for 1%).</summary>
    public decimal UifEmployerRate { get; }

    /// <summary>Monthly remuneration ceiling for UIF calculation (e.g., 17712.00 ZAR).</summary>
    public decimal UifMonthlyCeiling { get; }

    /// <summary>Maximum employee UIF contribution per month (e.g., R177.12).</summary>
    public decimal MaxEmployeeMonthly { get; }

    /// <summary>Maximum employer UIF contribution per month (e.g., R177.12).</summary>
    public decimal MaxEmployerMonthly { get; }

    /// <summary>SDL rate (e.g., 0.01 for 1%). Employer-only.</summary>
    public decimal SdlRate { get; }

    /// <summary>
    /// Annual payroll threshold below which the employer is exempt from SDL (e.g., R500,000).
    /// PRD-16 Section 6: SDL exempt if annual leviable payroll &lt; this value.
    /// </summary>
    public decimal SdlExemptionThresholdAnnual { get; }

    private SarsUifSdlRuleSet(
        decimal uifEmpRate, decimal uifErRate, decimal uifCeiling,
        decimal maxEmpMonthly, decimal maxErMonthly,
        decimal sdlRate, decimal sdlExempt)
    {
        UifEmployeeRate = uifEmpRate;
        UifEmployerRate = uifErRate;
        UifMonthlyCeiling = uifCeiling;
        MaxEmployeeMonthly = maxEmpMonthly;
        MaxEmployerMonthly = maxErMonthly;
        SdlRate = sdlRate;
        SdlExemptionThresholdAnnual = sdlExempt;
    }

    /// <summary>
    /// Constructs a typed rule set from a raw <see cref="StatutoryRuleSet"/>.
    /// Throws if the domain is wrong or required keys are absent.
    /// CTL-SARS-002
    /// </summary>
    public static SarsUifSdlRuleSet From(StatutoryRuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        if (ruleSet.RuleDomain != RuleDomains.SarsUifSdl)
            throw new InvalidOperationException(
                $"Expected RuleDomain '{RuleDomains.SarsUifSdl}' but got '{ruleSet.RuleDomain}'.");

        var data = ruleSet.RuleData;
        var uif = StatutoryDataConverter.GetDict(data, "uif");
        var sdl = StatutoryDataConverter.GetDict(data, "sdl");

        return new SarsUifSdlRuleSet(
            uifEmpRate: StatutoryDataConverter.ToDecimal(uif["employee_rate"]),
            uifErRate:  StatutoryDataConverter.ToDecimal(uif["employer_rate"]),
            uifCeiling: StatutoryDataConverter.ToDecimal(uif["monthly_ceiling"]),
            maxEmpMonthly: StatutoryDataConverter.ToDecimal(uif["max_employee_monthly"]),
            maxErMonthly:  StatutoryDataConverter.ToDecimal(uif["max_employer_monthly"]),
            sdlRate:   StatutoryDataConverter.ToDecimal(sdl["rate"]),
            sdlExempt: StatutoryDataConverter.ToDecimal(sdl["exemption_threshold_annual"]));
    }

    /// <summary>Creates a rule set directly from typed values. Used in unit tests only.</summary>
    public static SarsUifSdlRuleSet CreateForTesting(
        decimal uifEmployeeRate = 0.01m,
        decimal uifEmployerRate = 0.01m,
        decimal uifMonthlyCeiling = 17712.00m,
        decimal maxEmployeeMonthly = 177.12m,
        decimal maxEmployerMonthly = 177.12m,
        decimal sdlRate = 0.01m,
        decimal sdlExemptionThresholdAnnual = 500000.00m) =>
        new(uifEmployeeRate, uifEmployerRate, uifMonthlyCeiling,
            maxEmployeeMonthly, maxEmployerMonthly, sdlRate, sdlExemptionThresholdAnnual);
}
