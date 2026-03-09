// REQ-HR-004, CTL-SARS-005: PDF payslip generator interface.
// CTL-BCEA-006: Payslip must be issued within 3 days of payment (BCEA §33).

namespace ZenoHR.Infrastructure.Services.Payslip;

/// <summary>
/// Generates a BCEA §33-compliant A4 PDF payslip for a single employee pay period.
/// </summary>
public interface IPayslipPdfGenerator
{
    /// <summary>
    /// Generates an A4 PDF payslip for a single employee pay period.
    /// Returns the raw PDF bytes.
    /// </summary>
    /// <param name="data">All required payslip data (BCEA §33 mandatory fields).</param>
    /// <returns>Raw PDF bytes beginning with the <c>%PDF</c> header.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the payslip invariant is violated:
    /// <c>net_pay != gross_pay - total_deductions</c>.
    /// </exception>
    byte[] Generate(PayslipData data);
}
