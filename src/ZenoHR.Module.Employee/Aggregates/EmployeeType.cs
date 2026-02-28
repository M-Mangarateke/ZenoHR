// REQ-HR-001: Employee type classification for payroll and BCEA compliance.

namespace ZenoHR.Module.Employee.Aggregates;

/// <summary>
/// Classification of the employment relationship. Affects BCEA protections and ETI eligibility.
/// </summary>
public enum EmployeeType
{
    Unknown = 0,
    Permanent = 1,
    PartTime = 2,
    Contractor = 3,
    Intern = 4,
}
