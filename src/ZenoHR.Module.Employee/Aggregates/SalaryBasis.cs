// REQ-HR-003: Salary basis determines payroll period handling per PRD-16.

namespace ZenoHR.Module.Employee.Aggregates;

/// <summary>
/// How the base salary amount is denominated. Determines payroll calculation period.
/// </summary>
public enum SalaryBasis
{
    Unknown = 0,
    Monthly = 1,
    Weekly = 2,
    Hourly = 3,
}
