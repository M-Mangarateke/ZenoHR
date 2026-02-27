// REQ-HR-003: ETI qualifying tier based on months of employment.
// CTL-SARS-003: Tier1 = first 12 qualifying months; Tier2 = months 13–24.
// PRD-16 Section 5: GetETITier(employmentStartDate, calculationDate).

namespace ZenoHR.Module.Payroll.Calculation;

/// <summary>
/// Employment Tax Incentive qualifying tier, determined by the number of completed calendar
/// months since the <c>employment_contracts.etiquette_start_date</c>.
/// Tier1 applies higher ETI rates; Tier2 applies reduced rates.
/// After 24 months the employee is no longer eligible.
/// CTL-SARS-003, PRD-16 Section 5.
/// </summary>
public enum EtiTier
{
    /// <summary>Default guard — employee not eligible for ETI.</summary>
    Ineligible = 0,

    /// <summary>First 12 qualifying months of employment (higher ETI rates).</summary>
    Tier1 = 1,

    /// <summary>Months 13–24 of qualifying employment (reduced ETI rates).</summary>
    Tier2 = 2,
}
