// CTL-SARS-006: EMP201 monthly declaration data model — PAYE + SDL + UIF
// SARS EMP201 field layout: docs/seed-data/sars-filing-formats/emp201-field-layout.json
// All monetary values are decimal (critical rule: no float/double).

namespace ZenoHR.Infrastructure.Services.Filing.Emp201;

/// <summary>
/// Root data model for a SARS EMP201 monthly PAYE/UIF/SDL declaration.
/// CTL-SARS-006: Employer monthly reconciliation submitted by the 7th of the following month.
/// </summary>
public sealed record Emp201Data
{
    public required string EmployerPAYEReference { get; init; }
    public required string EmployerUifReference { get; init; }
    public required string EmployerSdlReference { get; init; }
    public required string EmployerTradingName { get; init; }
    public required string TaxPeriod { get; init; }          // YYYYMM
    public required string PeriodLabel { get; init; }
    public required string PayrollRunId { get; init; }
    public required decimal TotalPayeDeducted { get; init; }
    public required decimal TotalUifEmployee { get; init; }
    public required decimal TotalUifEmployer { get; init; }
    public decimal TotalUif => TotalUifEmployee + TotalUifEmployer;
    public required decimal TotalSdl { get; init; }
    public required decimal TotalGrossRemuneration { get; init; }
    public required int EmployeeCount { get; init; }
    public required DateOnly DueDate { get; init; }
    public required IReadOnlyList<Emp201EmployeeLine> EmployeeLines { get; init; }
    public required string GeneratedByUserId { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
}

/// <summary>
/// Per-employee line within an EMP201 declaration.
/// CTL-SARS-006: One line per employee per payroll period.
/// </summary>
public sealed record Emp201EmployeeLine
{
    public required string EmployeeId { get; init; }
    public required string EmployeeFullName { get; init; }
    public required string TaxReferenceNumber { get; init; }
    public required string IdOrPassportNumber { get; init; }
    public required decimal GrossRemuneration { get; init; }
    public required decimal PayeDeducted { get; init; }
    public required decimal UifEmployee { get; init; }
    public required decimal UifEmployer { get; init; }
    public required decimal SdlEmployer { get; init; }
    public required string PaymentMethod { get; init; }
}
