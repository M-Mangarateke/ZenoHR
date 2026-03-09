// REQ-COMP-002, CTL-SARS-006
// EMP501 annual reconciliation input data models.
// South African tax year: March of previous year to February of current year.
// e.g. TaxYear "2026" = March 2025 – February 2026.

namespace ZenoHR.Infrastructure.Services.Filing.Emp501;

/// <summary>Input data for EMP501 annual reconciliation generation.</summary>
public sealed record Emp501Data
{
    public required string TenantId { get; init; }
    public required string EmployerTaxRef { get; init; }       // PAYE reference number
    public required string EmployerName { get; init; }
    public required string EmployerAddress { get; init; }
    public required string TaxYear { get; init; }              // e.g. "2026" (March 2025 – Feb 2026)
    public required IReadOnlyList<Emp201MonthlyEntry> MonthlySubmissions { get; init; }
    public required IReadOnlyList<Emp501EmployeeEntry> EmployeeEntries { get; init; }
}

/// <summary>One EMP201 monthly filing's declared totals.</summary>
public sealed record Emp201MonthlyEntry
{
    public required string Period { get; init; }               // "YYYY-MM"
    public required decimal TotalGross { get; init; }
    public required decimal TotalPaye { get; init; }
    public required decimal TotalUifEmployee { get; init; }
    public required decimal TotalUifEmployer { get; init; }
    public required decimal TotalSdl { get; init; }
    public required bool Filed { get; init; }
    public DateOnly? FiledDate { get; init; }
}

/// <summary>Per-employee annual summary for IRP5/IT3a reconciliation.</summary>
public sealed record Emp501EmployeeEntry
{
    public required string EmployeeId { get; init; }
    public required string EmployeeName { get; init; }
    public required string IdNumber { get; init; }             // SA ID or passport
    public required string TaxRef { get; init; }               // Employee SARS tax ref
    public required decimal AnnualGross { get; init; }
    public required decimal AnnualPaye { get; init; }
    public required decimal AnnualUifEmployee { get; init; }
    public required decimal AnnualUifEmployer { get; init; }
    public required decimal AnnualSdl { get; init; }
    public required decimal AnnualEti { get; init; }
    public required decimal AnnualMedicalCredit { get; init; } // MSFTC / section 6A credit
    public required string Irp5Code { get; init; }             // "IRP5" or "IT3a"
    public required string CertificateNumber { get; init; }    // Unique per employee per year
}
