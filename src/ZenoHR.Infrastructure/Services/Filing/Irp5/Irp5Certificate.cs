// CTL-SARS-008: IRP5/IT3(a) annual tax certificate data model.
// IRP5: issued when PAYE > 0 (tax was deducted from employee remuneration).
// IT3(a): issued when no PAYE was deducted (employee below tax threshold).
// Both form part of the EMP501 annual reconciliation submitted to SARS.
// All monetary values use MoneyZAR (decimal). No float/double — Sev-1 rule.

using ZenoHR.Domain.Common;

namespace ZenoHR.Infrastructure.Services.Filing.Irp5;

/// <summary>
/// Represents one IRP5 or IT3(a) annual tax certificate for a single employee
/// for a full South African tax year (1 March – last day of February).
/// CTL-SARS-008: Generated as part of EMP501 annual reconciliation.
/// </summary>
public sealed record Irp5Certificate
{
    // ── Certificate identity ──────────────────────────────────────────────────

    /// <summary>"IRP5" if PAYE > 0; "IT3a" if no PAYE was deducted.</summary>
    public required string CertificateType { get; init; }

    /// <summary>
    /// Deterministic certificate number: {TenantId}-{TaxYear}-{EmployeeId}.
    /// Determinism ensures identical output for the same input — required for reconciliation.
    /// </summary>
    public required string CertificateNumber { get; init; }

    /// <summary>South African tax year label, e.g. "2026" = 1 March 2025 – 28 February 2026.</summary>
    public required string TaxYear { get; init; }

    // ── Employer/employee identity ────────────────────────────────────────────

    public required string TenantId { get; init; }
    public required string EmployeeId { get; init; }
    public required string EmployeeName { get; init; }

    /// <summary>South African national ID number or passport number.</summary>
    public required string IdNumber { get; init; }

    /// <summary>Employee SARS income tax reference number.</summary>
    public required string TaxReferenceNumber { get; init; }

    // ── Tax year period ───────────────────────────────────────────────────────

    /// <summary>First day of the SA tax year (1 March of taxYear-1).</summary>
    public required DateOnly PeriodStart { get; init; }

    /// <summary>Last day of the SA tax year (last day of February of taxYear; handles leap year).</summary>
    public required DateOnly PeriodEnd { get; init; }

    // ── Income codes (SARS remuneration codes) ────────────────────────────────

    /// <summary>Code 3601: Basic salary / remuneration.</summary>
    public required MoneyZAR Code3601 { get; init; }

    /// <summary>Code 3605: Annual payment / bonus. Always zero in v1 (no bonus tracking).</summary>
    public required MoneyZAR Code3605 { get; init; }

    /// <summary>Code 3713: Travel / subsistence allowances (grouped).</summary>
    public required MoneyZAR Code3713 { get; init; }

    /// <summary>Code 3697: Other additions / reimbursements.</summary>
    public required MoneyZAR Code3697 { get; init; }

    // ── Deduction codes (SARS deduction codes) ────────────────────────────────

    /// <summary>Code 4001: PAYE deducted from employee.</summary>
    public required MoneyZAR Code4001 { get; init; }

    /// <summary>Code 4005: UIF employee contribution.</summary>
    public required MoneyZAR Code4005 { get; init; }

    /// <summary>Code 4474: Pension fund employee contribution.</summary>
    public required MoneyZAR Code4474 { get; init; }

    /// <summary>Code 4493: Medical aid employee contribution.</summary>
    public required MoneyZAR Code4493 { get; init; }

    /// <summary>Code 4497: Other deductions (catch-all).</summary>
    public required MoneyZAR Code4497 { get; init; }

    // ── Derived totals ────────────────────────────────────────────────────────

    /// <summary>Sum of all income codes (3601 + 3605 + 3713 + 3697).</summary>
    public required MoneyZAR TotalRemuneration { get; init; }

    /// <summary>Sum of all deduction codes (4001 + 4005 + 4474 + 4493 + 4497).</summary>
    public required MoneyZAR TotalDeductions { get; init; }

    /// <summary>
    /// Taxable income = TotalRemuneration (simplified — exempt allowances not tracked in v1).
    /// </summary>
    public required MoneyZAR TaxableIncome { get; init; }

    // ── Metadata ──────────────────────────────────────────────────────────────

    public required DateTimeOffset GeneratedAt { get; init; }
}
