// CTL-SARS-005: EMP601 certificate cancellation declaration.
// EMP601 is used to cancel previously issued IRP5/IT3(a) certificates.
// Use cases: duplicates, data errors, employee disputes.
// All monetary values use MoneyZAR (decimal-backed) — never float/double.

using ZenoHR.Domain.Common;

namespace ZenoHR.Infrastructure.Services.Filing.Emp601;

/// <summary>
/// Represents a single EMP601 certificate cancellation entry for one employee.
/// CTL-SARS-005: Each record cancels one previously issued IRP5 or IT3(a) certificate.
/// </summary>
public sealed record Emp601Record(
    string EmployeeId,
    string EmployeeName,
    string IdNumber,                        // National ID or passport number
    string TaxReferenceNumber,
    string OriginalCertificateNumber,       // The IRP5/IT3(a) cert being cancelled
    string CancellationReason,              // e.g. "DUPLICATE", "DATA_ERROR", "EMPLOYEE_DISPUTE"
    string TaxYear,                         // e.g. "2026"
    DateOnly CancellationDate,
    MoneyZAR OriginalPayeAmount,            // PAYE from original certificate
    MoneyZAR OriginalGrossAmount,           // Gross remuneration from original certificate
    string? ReplacementCertificateNumber    // Optional: replacement cert number if cert is being replaced
);
