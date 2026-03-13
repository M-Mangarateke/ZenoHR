// CTL-BCEA-001, CTL-BCEA-003: BCEA statutory limits — injectable, not hardcoded.
// Critical Rule #1: All statutory values come from StatutoryRuleSet, never literals in code.

namespace ZenoHR.Module.Payroll.Models;

/// <summary>
/// BCEA working-time and leave compliance limits.
/// Values should be sourced from StatutoryRuleSet in Firestore at runtime.
/// Defaults match the current BCEA Act values for bootstrapping / tests only.
/// </summary>
public sealed record BceaComplianceOptions
{
    /// <summary>BCEA Section 9 — maximum ordinary hours per week.</summary>
    public decimal MaxOrdinaryHoursPerWeek { get; init; } = 45m;

    /// <summary>BCEA Section 10 — maximum overtime hours per week (with written agreement).</summary>
    public decimal MaxOvertimeHoursPerWeek { get; init; } = 10m;

    /// <summary>BCEA Section 20 — annual leave entitlement in working days.</summary>
    public decimal AnnualLeaveEntitlementDays { get; init; } = 15m;

    /// <summary>Derived: maximum total hours per week (ordinary + overtime).</summary>
    public decimal MaxTotalHoursWithAgreement => MaxOrdinaryHoursPerWeek + MaxOvertimeHoursPerWeek;

    /// <summary>Derived: monthly leave accrual rate (annual entitlement / 12).</summary>
    public decimal MonthlyLeaveAccrualRate => AnnualLeaveEntitlementDays / 12m;
}
