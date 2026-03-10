// CTL-SARS-004: EMP701 prior-year reconciliation adjustment.
// EMP701 is submitted when an employer discovers discrepancies in a previously submitted
// EMP501 annual reconciliation and needs to correct values for a prior tax year.
// All monetary values use MoneyZAR (decimal-backed). Differences may be negative (overpayment).

using ZenoHR.Domain.Common;

namespace ZenoHR.Infrastructure.Services.Filing.Emp701;

/// <summary>
/// Represents a single employee adjustment record in a SARS EMP701 prior-year reconciliation.
/// CTL-SARS-004: Corrects discrepancies in a previously submitted EMP501 for a prior tax year.
/// </summary>
public sealed record Emp701AdjustmentRecord(
    string EmployeeId,
    string EmployeeName,
    string IdNumber,
    string TaxReferenceNumber,
    string OriginalCertificateNumber,
    string AdjustmentReason,
    string TaxYear,
    MoneyZAR OriginalPayeAmount,
    MoneyZAR AdjustedPayeAmount,
    MoneyZAR OriginalGrossAmount,
    MoneyZAR AdjustedGrossAmount,
    MoneyZAR OriginalUifAmount,
    MoneyZAR AdjustedUifAmount,
    DateOnly AdjustmentDate,
    string? Notes)
{
    // CTL-SARS-004: Computed differences — may be negative when correcting an overpayment.

    /// <summary>Difference in PAYE: positive = additional liability, negative = overpayment.</summary>
    public MoneyZAR PayeDifference => AdjustedPayeAmount - OriginalPayeAmount;

    /// <summary>Difference in gross remuneration: positive = underdeclared, negative = overdeclared.</summary>
    public MoneyZAR GrossDifference => AdjustedGrossAmount - OriginalGrossAmount;

    /// <summary>Difference in UIF: positive = shortfall, negative = overpayment.</summary>
    public MoneyZAR UifDifference => AdjustedUifAmount - OriginalUifAmount;
}
