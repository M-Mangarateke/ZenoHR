// REQ-HR-003: Pay frequency determines PAYE annualisation method.
// PRD-16 Section 1 (Monthly ÷12/×12) and Section 3 (Weekly ÷52/×52).

namespace ZenoHR.Module.Payroll.Calculation;

/// <summary>
/// Pay frequency for a payroll run. Determines how taxable income is annualised
/// and de-annualised for PAYE and UIF/SDL calculations.
/// ZenoHR v1 supports Monthly and Weekly (ASSUME-006 override, confirmed 2026-02-19).
/// </summary>
public enum PayFrequency
{
    /// <summary>Default guard value — must not be used in calculations.</summary>
    Unknown = 0,

    /// <summary>Monthly payroll. PAYE: ×12 to annualise, ÷12 to de-annualise.</summary>
    Monthly = 1,

    /// <summary>Weekly payroll. PAYE: ×52 to annualise, ÷52 to de-annualise.</summary>
    Weekly = 2,
}
