// REQ-HR-003, REQ-HR-004, CTL-SARS-001: Payslip invariant: net_pay == gross_pay - deductions + additions.
// Any cent mismatch is a Sev-1 defect and must block payroll finalization.
// See CLAUDE.md Payroll Calculation Rules and PRD-16 Section 9.
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Module.Payroll.Entities;

/// <summary>
/// Verifies the payslip net-pay invariant to the cent.
/// <para>
/// Invariant: <c>net_pay == gross_pay - deduction_total + addition_total</c>
/// </para>
/// A mismatch of even one cent indicates a calculation bug and must block finalization.
/// </summary>
public static class PayslipInvariantVerifier
{
    /// <summary>
    /// Verifies the payslip invariant.
    /// Returns <see cref="Result{T}"/> failure with <see cref="ZenoHrErrorCode.PayslipInvariantViolation"/>
    /// if the invariant is broken.
    /// </summary>
    /// <param name="grossPay">Total gross pay (basic + overtime + allowances).</param>
    /// <param name="deductionTotal">Sum of all deductions.</param>
    /// <param name="additionTotal">Sum of all non-gross additions (reimbursements, etc.).</param>
    /// <param name="netPay">Stated net pay.</param>
    public static Result<bool> Verify(
        MoneyZAR grossPay,
        MoneyZAR deductionTotal,
        MoneyZAR additionTotal,
        MoneyZAR netPay)
    {
        var expected = grossPay - deductionTotal + additionTotal;

        // Compare via decimal value to catch floating-point-style rounding mismatches.
        if (expected.Amount != netPay.Amount)
        {
            var diff = (expected - netPay).Amount;
            return Result<bool>.Failure(
                ZenoHrErrorCode.PayslipInvariantViolation,
                $"Payslip invariant violated: expected net pay R{expected.Amount:F2}, " +
                $"got R{netPay.Amount:F2} (diff = R{diff:F2}). " +
                "This is a Sev-1 defect — investigation required before finalization.");
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Verifies the payslip invariant using raw decimal values (for use in calculation engines).
    /// </summary>
    public static Result<bool> VerifyDecimal(
        decimal grossPay,
        decimal deductionTotal,
        decimal additionTotal,
        decimal netPay)
    {
        var expected = grossPay - deductionTotal + additionTotal;
        if (expected != netPay)
        {
            var diff = expected - netPay;
            return Result<bool>.Failure(
                ZenoHrErrorCode.PayslipInvariantViolation,
                $"Payslip invariant violated: expected net pay R{expected:F2}, " +
                $"got R{netPay:F2} (diff = R{diff:F2}).");
        }
        return Result<bool>.Success(true);
    }
}
