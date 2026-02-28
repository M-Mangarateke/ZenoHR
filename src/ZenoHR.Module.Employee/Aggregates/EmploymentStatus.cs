// REQ-HR-001: Employment status lifecycle for SA labour law compliance.
// Terminated employees are never deleted — records are retained per POPIA.

namespace ZenoHR.Module.Employee.Aggregates;

/// <summary>
/// Lifecycle state of an employee's employment. Governs payroll inclusion and leave accrual.
/// </summary>
public enum EmploymentStatus
{
    Unknown = 0,

    /// <summary>Currently employed and active on payroll.</summary>
    Active = 1,

    /// <summary>Employment suspended (e.g. pending disciplinary outcome). Not included in payroll runs.</summary>
    Suspended = 2,

    /// <summary>
    /// Employment ended. Record retained per POPIA data retention rules.
    /// Excluded from all future payroll runs.
    /// </summary>
    Terminated = 3,
}
