// REQ-HR-003: Other deductions and additions on a payslip (e.g. loan repayments, bonuses).

namespace ZenoHR.Module.Payroll.Entities;

/// <summary>
/// An additional deduction or addition line item on a payslip.
/// Used for items that do not have a dedicated field (e.g. study loan, travel allowance).
/// </summary>
/// <param name="Code">Short identifier for the line item type (e.g., "LOAN", "BONUS").</param>
/// <param name="Description">Human-readable description shown on the payslip.</param>
/// <param name="AmountZar">Amount in ZAR. Always positive; deduction/addition context determined by the containing list.</param>
public sealed record OtherLineItem(
    string Code,
    string Description,
    decimal AmountZar);
