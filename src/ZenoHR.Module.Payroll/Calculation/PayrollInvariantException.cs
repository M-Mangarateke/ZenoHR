// REQ-HR-003: Payslip invariant — net_pay must equal gross minus all deductions, to the cent.
// PRD-16 Section 9: PayrollInvariantException halts the entire payroll run (Sev-1).
// Critical rule: Never catch and suppress this exception. Halt run, flag error, require manual review.

namespace ZenoHR.Module.Payroll.Calculation;

/// <summary>
/// Thrown when the payslip arithmetic invariant is violated:
/// <c>netPay ≠ grossPay − paye − uifEmployee − pensionEmployee − medicalEmployee − otherDeductions + otherAdditions</c>
/// <para>
/// This is a <strong>Sev-1 defect</strong>. The entire payroll run must halt.
/// No <see cref="PayrollResult"/> documents are written to Firestore.
/// The <c>PayrollRun.status</c> stays at <c>Processing</c> and is flagged with <c>error: invariant_violation</c>.
/// </para>
/// PRD-16 Section 9.
/// </summary>
public sealed class PayrollInvariantException : Exception
{
    /// <summary>The employee ID whose payslip failed the invariant check.</summary>
    public string EmployeeId { get; }

    /// <summary>The payroll run ID in which the violation occurred.</summary>
    public string PayrollRunId { get; }

    /// <summary>The computed expected net pay.</summary>
    public decimal ExpectedNetPay { get; }

    /// <summary>The actual net pay stored in the result.</summary>
    public decimal ActualNetPay { get; }

    public PayrollInvariantException(
        string employeeId, string payrollRunId, decimal expectedNetPay, decimal actualNetPay)
        : base(
            $"Payslip invariant violated for employee '{employeeId}' in run '{payrollRunId}'. " +
            $"Expected netPay={expectedNetPay:F2}, actual netPay={actualNetPay:F2}. " +
            $"Halting payroll run — no results written.")
    {
        EmployeeId = employeeId;
        PayrollRunId = payrollRunId;
        ExpectedNetPay = expectedNetPay;
        ActualNetPay = actualNetPay;
    }
}
