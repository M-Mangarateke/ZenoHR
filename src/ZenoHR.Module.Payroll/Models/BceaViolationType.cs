// VUL-024, VUL-025: BCEA violation types for pre-payroll compliance checks.
// CTL-BCEA-001, CTL-BCEA-003

namespace ZenoHR.Module.Payroll.Models;

/// <summary>
/// Types of BCEA compliance violations detected during pre-payroll validation.
/// </summary>
public enum BceaViolationType
{
    Unknown = 0,
    OvertimeExceeded = 1,
    NoOvertimeAgreement = 2,
    OrdinaryHoursExceeded = 3,
    LeaveBalanceBelowMinimum = 4
}
