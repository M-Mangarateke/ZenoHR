// REQ-HR-004, CTL-SARS-005: Payslip data model — all BCEA Section 33 mandatory fields.
// CTL-BCEA-006: Payslip must be issued within 3 days of payment.
// All monetary values are decimal (critical rule: no float/double).

namespace ZenoHR.Infrastructure.Services.Payslip;

/// <summary>All data required to generate a BCEA §33 compliant payslip.</summary>
public sealed record PayslipData
{
    // ── Employer (BCEA §33(1)(a)) ─────────────────────────────────────────
    public required string EmployerName { get; init; }
    public required string EmployerRegistrationNumber { get; init; }
    public required string EmployerAddress { get; init; }
    public required string EmployerTaxReferenceNumber { get; init; }
    public required string EmployerPayeReference { get; init; }
    public required string EmployerUifReferenceNumber { get; init; }

    // ── Employee (BCEA §33(1)(b)) ─────────────────────────────────────────
    public required string EmployeeId { get; init; }
    public required string EmployeeFullName { get; init; }
    public required string JobTitle { get; init; }
    public required string Department { get; init; }
    public required string TaxReferenceNumber { get; init; }
    public required string UifNumber { get; init; }
    public required string IdOrPassportMasked { get; init; }
    public required DateOnly HireDate { get; init; }
    public required string PaymentMethod { get; init; }

    // ── Pay period (BCEA §33(1)(c)) ──────────────────────────────────────
    public required string PayPeriodLabel { get; init; }
    public required DateOnly PeriodStart { get; init; }
    public required DateOnly PeriodEnd { get; init; }
    public required DateOnly PaymentDate { get; init; }
    public required string PayrollRunReference { get; init; }

    // ── Hours (BCEA §33: ordinary_hours, overtime_hours) ──────────────────
    public decimal HoursOrdinary { get; init; }
    public decimal HoursOvertime { get; init; }

    // ── Earnings (BCEA §33(1)(d)) ─────────────────────────────────────────
    public required decimal BasicSalary { get; init; }
    public decimal Overtime { get; init; }
    public decimal TravelAllowance { get; init; }
    public decimal MedicalAidEmployerContribution { get; init; }
    public decimal PensionEmployerContribution { get; init; }
    public decimal Bonus { get; init; }
    public decimal OtherEarnings { get; init; }
    public string? OtherEarningsLabel { get; init; }
    public required decimal GrossSalary { get; init; }

    // ── Deductions (BCEA §33(1)(e)) ──────────────────────────────────────
    public required decimal PayeAmount { get; init; }
    public required decimal UifEmployee { get; init; }
    public decimal PensionEmployee { get; init; }
    public decimal MedicalAidEmployee { get; init; }
    public decimal OtherDeductions { get; init; }
    public string? OtherDeductionsLabel { get; init; }
    public required decimal TotalDeductions { get; init; }

    // ── Net pay (BCEA §33(1)(e) — actual amount paid) ─────────────────────
    public required decimal NetPay { get; init; }

    // ── Employer-side contributions (informational — not deducted) ────────
    public decimal UifEmployer { get; init; }
    public decimal Sdl { get; init; }
    public decimal EtiAmount { get; init; }

    // ── Tax summary (IRP5 codes) ──────────────────────────────────────────
    public required decimal AnnualisedIncome { get; init; }
    public required decimal AnnualTaxLiability { get; init; }
    public required decimal PrimaryRebate { get; init; }
    public decimal AgeRebate { get; init; }
    public required decimal YtdPaye { get; init; }
    public required decimal YtdUifEmployee { get; init; }
    public required decimal YtdGross { get; init; }

    // ── Leave balances (BCEA §33(1)(f)) ──────────────────────────────────
    public decimal AnnualLeaveBalance { get; init; }
    public decimal AnnualLeaveEntitlement { get; init; }
    public decimal SickLeaveBalance { get; init; }
    public decimal SickLeaveEntitlement { get; init; }
    public decimal FamilyResponsibilityBalance { get; init; }
    public decimal FamilyResponsibilityEntitlement { get; init; }

    // ── BCEA statement ────────────────────────────────────────────────────
    public required string TaxYear { get; init; }
    public required string PayFrequency { get; init; }

    // ── Audit ─────────────────────────────────────────────────────────────
    public required string GeneratedByUserId { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required string PayrollRunId { get; init; }
    public required string PayrollResultId { get; init; }
}
