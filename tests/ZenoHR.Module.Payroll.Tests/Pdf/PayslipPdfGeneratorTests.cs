// TC-PAY-030: QuestPDF payslip generator — BCEA §33 compliance, invariant enforcement.
// REQ-HR-004, CTL-SARS-005: Payslip PDF generation.
// CTL-BCEA-006: Payslip must include all BCEA Section 33 mandatory fields.

using FluentAssertions;
using ZenoHR.Infrastructure.Services.Payslip;

namespace ZenoHR.Module.Payroll.Tests.Pdf;

/// <summary>
/// Unit tests for <see cref="PayslipPdfGenerator"/>.
/// Tests cover: PDF output validity, invariant enforcement, edge cases, BCEA field presence.
/// </summary>
public sealed class PayslipPdfGeneratorTests
{
    private readonly IPayslipPdfGenerator _generator = new PayslipPdfGenerator();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal, fully-valid <see cref="PayslipData"/> for a Zenowethu employee.
    /// All monetary values satisfy the payslip invariant: net_pay == gross - deductions.
    /// </summary>
    private static PayslipData ValidPayslipData(
        decimal basicSalary = 50_000m,
        decimal paye = 14_921.54m,
        decimal uifEmployee = 177.12m,
        decimal pensionEmployee = 3_750m,
        decimal medicalEmployee = 3_750m,
        decimal otherDeductions = 0m) =>
        BuildData(basicSalary, paye, uifEmployee, pensionEmployee, medicalEmployee, otherDeductions);

    private static PayslipData BuildData(
        decimal basicSalary,
        decimal paye,
        decimal uifEmployee,
        decimal pensionEmployee,
        decimal medicalEmployee,
        decimal otherDeductions)
    {
        var gross = basicSalary;
        var totalDeductions = paye + uifEmployee + pensionEmployee + medicalEmployee + otherDeductions;
        var netPay = gross - totalDeductions;

        return new PayslipData
        {
            // Employer
            EmployerName = "Zenowethu (Pty) Ltd",
            EmployerRegistrationNumber = "2018/123456/07",
            EmployerAddress = "23 Innovation Drive, Sandton, Gauteng, 2196",
            EmployerTaxReferenceNumber = "9123456789",
            EmployerPayeReference = "7234567890",
            EmployerUifReferenceNumber = "U0987654321",

            // Employee
            EmployeeId = "EMP-0182",
            EmployeeFullName = "Lerato Dlamini",
            JobTitle = "Senior Developer",
            Department = "Engineering",
            TaxReferenceNumber = "9123456789",
            UifNumber = "UIF-0182",
            IdOrPassportMasked = "890215****086",
            HireDate = new DateOnly(2022, 3, 1),
            PaymentMethod = "EFT — ABSA ****1234",

            // Pay period
            PayPeriodLabel = "March 2026",
            PeriodStart = new DateOnly(2026, 3, 1),
            PeriodEnd = new DateOnly(2026, 3, 31),
            PaymentDate = new DateOnly(2026, 3, 28),
            PayrollRunReference = "RUN-2026-03",

            // Hours
            HoursOrdinary = 173.33m,
            HoursOvertime = 8m,

            // Earnings
            BasicSalary = basicSalary,
            Overtime = 0m,
            TravelAllowance = 0m,
            MedicalAidEmployerContribution = 3_750m,
            PensionEmployerContribution = 3_750m,
            GrossSalary = gross,

            // Deductions
            PayeAmount = paye,
            UifEmployee = uifEmployee,
            PensionEmployee = pensionEmployee,
            MedicalAidEmployee = medicalEmployee,
            OtherDeductions = otherDeductions,
            TotalDeductions = totalDeductions,

            // Net pay
            NetPay = netPay,

            // Employer-side
            UifEmployer = 177.12m,
            Sdl = 500m,
            EtiAmount = 0m,

            // Tax summary
            AnnualisedIncome = basicSalary * 12m,
            AnnualTaxLiability = paye * 12m,
            PrimaryRebate = 17_235m,
            YtdPaye = paye,
            YtdUifEmployee = uifEmployee,
            YtdGross = gross,

            // Leave balances
            AnnualLeaveBalance = 14m,
            AnnualLeaveEntitlement = 21m,
            SickLeaveBalance = 18m,
            SickLeaveEntitlement = 30m,
            FamilyResponsibilityBalance = 2m,
            FamilyResponsibilityEntitlement = 3m,

            // Metadata
            TaxYear = "2025/2026",
            PayFrequency = "Monthly",
            GeneratedByUserId = "user_hr_001",
            GeneratedAt = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero),
            PayrollRunId = "pr_2026_03_001",
            PayrollResultId = "res_EMP-0182_2026-03",
        };
    }

    // ── TC-PAY-030-001: Valid data produces a PDF ──────────────────────────────

    [Fact]
    public void Generate_ValidData_ReturnsPdfBytes()
    {
        // TC-PAY-030-001: Generator must produce non-empty bytes starting with %PDF header.
        var data = ValidPayslipData();

        var result = _generator.Generate(data);

        result.Should().NotBeNullOrEmpty("generator must return PDF bytes");
        result.Length.Should().BeGreaterThan(100, "a minimal PDF is always > 100 bytes");

        // Verify PDF magic bytes: "%PDF"
        var header = System.Text.Encoding.ASCII.GetString(result, 0, 4);
        header.Should().Be("%PDF", "PDF files must start with the %PDF header");
    }

    // ── TC-PAY-030-002: Invariant enforcement ─────────────────────────────────

    [Fact]
    public void Generate_NetPayInvariantViolated_ThrowsInvalidOperationException()
    {
        // TC-PAY-030-002: gross - deductions != net_pay must throw.
        var gross = 50_000m;
        var totalDeductions = 22_598.66m;
        // Deliberately wrong net pay — 1 rand off
        var wrongNetPay = gross - totalDeductions + 1m;

        var data = new PayslipData
        {
            EmployerName = "Zenowethu (Pty) Ltd",
            EmployerRegistrationNumber = "2018/123456/07",
            EmployerAddress = "23 Innovation Drive, Sandton, Gauteng, 2196",
            EmployerTaxReferenceNumber = "9123456789",
            EmployerPayeReference = "7234567890",
            EmployerUifReferenceNumber = "U0987654321",
            EmployeeId = "EMP-001",
            EmployeeFullName = "Test Employee",
            JobTitle = "Tester",
            Department = "QA",
            TaxReferenceNumber = "123",
            UifNumber = "UIF-001",
            IdOrPassportMasked = "****",
            HireDate = new DateOnly(2020, 1, 1),
            PaymentMethod = "EFT",
            PayPeriodLabel = "March 2026",
            PeriodStart = new DateOnly(2026, 3, 1),
            PeriodEnd = new DateOnly(2026, 3, 31),
            PaymentDate = new DateOnly(2026, 3, 28),
            PayrollRunReference = "RUN-TEST",
            BasicSalary = gross,
            GrossSalary = gross,
            PayeAmount = 14_921.54m,
            UifEmployee = 177.12m,
            TotalDeductions = totalDeductions,
            NetPay = wrongNetPay,  // WRONG — invariant violated
            AnnualisedIncome = gross * 12m,
            AnnualTaxLiability = 14_921.54m * 12m,
            PrimaryRebate = 17_235m,
            YtdPaye = 14_921.54m,
            YtdUifEmployee = 177.12m,
            YtdGross = gross,
            TaxYear = "2025/2026",
            PayFrequency = "Monthly",
            GeneratedByUserId = "user_test",
            GeneratedAt = DateTimeOffset.UtcNow,
            PayrollRunId = "pr_test",
            PayrollResultId = "res_test",
        };

        var act = () => _generator.Generate(data);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*invariant*");
    }

    // ── TC-PAY-030-003: Zero net pay (fully deducted) ─────────────────────────

    [Fact]
    public void Generate_ZeroNetPay_DoesNotThrow()
    {
        // TC-PAY-030-003: Edge case — entire salary deducted, net = 0.
        var basicSalary = 10_000m;
        var data = ValidPayslipData(
            basicSalary: basicSalary,
            paye: 5_000m,
            uifEmployee: 100m,
            pensionEmployee: 2_400m,
            medicalEmployee: 2_500m,
            otherDeductions: 0m);

        // Verify we're actually testing zero net
        data.NetPay.Should().Be(0m, "all earnings fully consumed by deductions");

        var act = () => _generator.Generate(data);
        act.Should().NotThrow("zero net pay is a valid (if unusual) payslip");
    }

    // ── TC-PAY-030-004: All BCEA §33 fields present ───────────────────────────

    [Fact]
    public void Generate_AllBceaFieldsPresent_ProducesPdf()
    {
        // TC-PAY-030-004: Fully populated data with all BCEA §33 fields must produce a valid PDF.
        var data = ValidPayslipData(
            basicSalary: 50_000m,
            paye: 14_921.54m,
            uifEmployee: 177.12m,
            pensionEmployee: 3_750m,
            medicalEmployee: 3_750m);

        var result = _generator.Generate(data);

        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
        var header = System.Text.Encoding.ASCII.GetString(result, 0, 4);
        header.Should().Be("%PDF");
    }

    // ── TC-PAY-030-005: Overtime present produces PDF ─────────────────────────

    [Fact]
    public void Generate_WithOvertimeAndAllowances_ReturnsPdfBytes()
    {
        // TC-PAY-030-005: Overtime + travel allowance rows render without error.
        var gross = 55_461.54m;
        var paye = 14_921.54m;
        var uif = 177.12m;
        var pension = 3_750m;
        var medical = 3_750m;
        var totalDeductions = paye + uif + pension + medical;
        var net = gross - totalDeductions;

        var data = new PayslipData
        {
            EmployerName = "Zenowethu (Pty) Ltd",
            EmployerRegistrationNumber = "2018/123456/07",
            EmployerAddress = "23 Innovation Drive, Sandton, Gauteng, 2196",
            EmployerTaxReferenceNumber = "9123456789",
            EmployerPayeReference = "7234567890",
            EmployerUifReferenceNumber = "U0987654321",
            EmployeeId = "EMP-0182",
            EmployeeFullName = "Lerato Dlamini",
            JobTitle = "Senior Developer",
            Department = "Engineering",
            TaxReferenceNumber = "9123456789",
            UifNumber = "UIF-0182",
            IdOrPassportMasked = "890215****086",
            HireDate = new DateOnly(2022, 3, 1),
            PaymentMethod = "EFT — Standard Bank ****4492",
            PayPeriodLabel = "March 2026",
            PeriodStart = new DateOnly(2026, 3, 1),
            PeriodEnd = new DateOnly(2026, 3, 31),
            PaymentDate = new DateOnly(2026, 3, 28),
            PayrollRunReference = "RUN-2026-03",
            HoursOrdinary = 173.33m,
            HoursOvertime = 8m,
            BasicSalary = 50_000m,
            Overtime = 3_461.54m,
            TravelAllowance = 2_000m,
            MedicalAidEmployerContribution = 3_750m,
            PensionEmployerContribution = 3_750m,
            GrossSalary = gross,
            PayeAmount = paye,
            UifEmployee = uif,
            PensionEmployee = pension,
            MedicalAidEmployee = medical,
            TotalDeductions = totalDeductions,
            NetPay = net,
            UifEmployer = 177.12m,
            Sdl = 554.62m,
            EtiAmount = 0m,
            AnnualisedIncome = gross * 12m,
            AnnualTaxLiability = paye * 12m,
            PrimaryRebate = 17_235m,
            YtdPaye = paye,
            YtdUifEmployee = uif,
            YtdGross = gross,
            AnnualLeaveBalance = 14m,
            AnnualLeaveEntitlement = 21m,
            SickLeaveBalance = 18m,
            SickLeaveEntitlement = 30m,
            FamilyResponsibilityBalance = 2m,
            FamilyResponsibilityEntitlement = 3m,
            TaxYear = "2025/2026",
            PayFrequency = "Monthly",
            GeneratedByUserId = "user_hr_001",
            GeneratedAt = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero),
            PayrollRunId = "pr_2026_03_001",
            PayrollResultId = "res_EMP-0182_2026-03",
        };

        var result = _generator.Generate(data);

        result.Should().NotBeNullOrEmpty();
        System.Text.Encoding.ASCII.GetString(result, 0, 4).Should().Be("%PDF");
    }
}
