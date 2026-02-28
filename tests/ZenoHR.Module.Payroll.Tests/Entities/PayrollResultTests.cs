// TC-PAY-025: PayrollResult entity — create validation, totals computation, reconstitution.
// REQ-HR-003, REQ-HR-004, CTL-SARS-001: Per-employee payslip result.
using FluentAssertions;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Payroll.Entities;

namespace ZenoHR.Module.Payroll.Tests.Entities;

public sealed class PayrollResultTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Result<PayrollResult> ValidCreate(
        string? employeeId = "emp_001",
        string? payrollRunId = "pr_001",
        string? tenantId = "tenant_001",
        decimal basicSalary = 30_000m,
        decimal overtimePay = 0m,
        decimal allowances = 0m,
        decimal paye = 4_000m,
        decimal uifEmployee = 177.12m,
        decimal uifEmployer = 177.12m,
        decimal sdl = 300m,
        decimal pensionEmployee = 0m,
        decimal pensionEmployer = 0m,
        decimal medicalEmployee = 0m,
        decimal medicalEmployer = 0m,
        decimal etiAmount = 0m,
        bool etiEligible = false,
        string? taxTableVersion = "v2026.1.0")
    {
        return PayrollResult.Create(
            employeeId!, payrollRunId!, tenantId!,
            basicSalary, overtimePay, allowances,
            paye, uifEmployee, uifEmployer, sdl,
            pensionEmployee, pensionEmployer,
            medicalEmployee, medicalEmployer,
            etiAmount, etiEligible,
            otherDeductions: null,
            otherAdditions: null,
            hoursOrdinary: 176m,
            hoursOvertime: 0m,
            taxTableVersion: taxTableVersion!,
            complianceFlags: null,
            calculationTimestamp: DateTimeOffset.UtcNow);
    }

    // ── Success: totals computation ───────────────────────────────────────────

    [Fact]
    public void Create_BasicSalaryOnly_ComputesTotalsCorrectly()
    {
        // TC-PAY-025-001 — gross = basic, deductions = PAYE + UIF, net = gross - deductions
        var result = ValidCreate(basicSalary: 30_000m, paye: 4_000m, uifEmployee: 177.12m);

        result.IsSuccess.Should().BeTrue();
        var v = result.Value!;
        v.GrossPay.Amount.Should().Be(30_000m);
        v.DeductionTotal.Amount.Should().Be(4_177.12m);   // 4_000 + 177.12
        v.NetPay.Amount.Should().Be(25_822.88m);           // 30_000 - 4_177.12
    }

    [Fact]
    public void Create_OvertimeAndAllowances_IncludedInGross()
    {
        // TC-PAY-025-002
        var result = ValidCreate(basicSalary: 25_000m, overtimePay: 3_750m, allowances: 1_000m,
            paye: 5_000m, uifEmployee: 177.12m);

        result.IsSuccess.Should().BeTrue();
        var v = result.Value!;
        v.GrossPay.Amount.Should().Be(29_750m);            // 25_000 + 3_750 + 1_000
        v.DeductionTotal.Amount.Should().Be(5_177.12m);
        v.NetPay.Amount.Should().Be(24_572.88m);
    }

    [Fact]
    public void Create_WithPensionAndMedical_IncludedInDeductions()
    {
        // TC-PAY-025-003
        var result = ValidCreate(basicSalary: 40_000m, paye: 8_000m, uifEmployee: 177.12m,
            pensionEmployee: 2_000m, medicalEmployee: 1_500m);

        result.IsSuccess.Should().BeTrue();
        var v = result.Value!;
        v.DeductionTotal.Amount.Should().Be(11_677.12m);  // 8_000 + 177.12 + 2_000 + 1_500
        v.NetPay.Amount.Should().Be(28_322.88m);
    }

    [Fact]
    public void Create_WithOtherDeductions_IncludedInDeductionTotal()
    {
        // TC-PAY-025-004 — other deductions reduce net
        var otherDeductions = new List<OtherLineItem>
        {
            new("LOAN_RECOVERY", "Salary advance recovery", 500m),
        };
        var result = PayrollResult.Create(
            "emp_001", "pr_001", "tenant_001",
            30_000m, 0m, 0m,
            4_000m, 177.12m, 177.12m, 300m,
            MoneyZAR.Zero, MoneyZAR.Zero, MoneyZAR.Zero, MoneyZAR.Zero,
            MoneyZAR.Zero, false,
            otherDeductions, null,
            176m, 0m, "v2026.1.0", null, DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DeductionTotal.Amount.Should().Be(4_677.12m); // 4_000 + 177.12 + 500
        result.Value.NetPay.Amount.Should().Be(25_322.88m);
    }

    [Fact]
    public void Create_WithOtherAdditions_IncreaseNetAboveGrossMinusDeductions()
    {
        // TC-PAY-025-005 — reimbursements added to net
        var otherAdditions = new List<OtherLineItem>
        {
            new("TRAVEL_REIMB", "Travel reimbursement", 800m),
        };
        var result = PayrollResult.Create(
            "emp_001", "pr_001", "tenant_001",
            30_000m, 0m, 0m,
            4_000m, 177.12m, 177.12m, 300m,
            MoneyZAR.Zero, MoneyZAR.Zero, MoneyZAR.Zero, MoneyZAR.Zero,
            MoneyZAR.Zero, false,
            null, otherAdditions,
            176m, 0m, "v2026.1.0", null, DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        result.Value!.NetPay.Amount.Should().Be(26_622.88m); // 25_822.88 + 800
    }

    [Fact]
    public void Create_EtiEligible_SetsEtiFields()
    {
        // TC-PAY-025-006
        var result = ValidCreate(etiAmount: 1_000m, etiEligible: true);

        result.IsSuccess.Should().BeTrue();
        result.Value!.EtiEligible.Should().BeTrue();
        result.Value.EtiAmount.Amount.Should().Be(1_000m);
    }

    // ── Validation failures ───────────────────────────────────────────────────

    [Fact]
    public void Create_EmptyEmployeeId_ReturnsValidationFailure()
    {
        // TC-PAY-025-010
        var result = ValidCreate(employeeId: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Contain("EmployeeId");
    }

    [Fact]
    public void Create_EmptyPayrollRunId_ReturnsValidationFailure()
    {
        // TC-PAY-025-011
        var result = ValidCreate(payrollRunId: "   ");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Contain("PayrollRunId");
    }

    [Fact]
    public void Create_EmptyTenantId_ReturnsValidationFailure()
    {
        // TC-PAY-025-012
        var result = ValidCreate(tenantId: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Contain("TenantId");
    }

    [Fact]
    public void Create_EmptyTaxTableVersion_ReturnsValidationFailure()
    {
        // TC-PAY-025-013
        var result = ValidCreate(taxTableVersion: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Contain("TaxTableVersion");
    }

    // ── Reconstitution ────────────────────────────────────────────────────────

    [Fact]
    public void Reconstitute_SetsAllProperties_FromFirestoreValues()
    {
        // TC-PAY-025-020 — read-path bypasses invariant re-check
        var ts = new DateTimeOffset(2026, 3, 31, 15, 0, 0, TimeSpan.Zero);

        var result = PayrollResult.Reconstitute(
            employeeId: "emp_fs_001",
            payrollRunId: "pr_fs_001",
            tenantId: "tenant_fs",
            basicSalary: 50_000m,
            overtimePay: MoneyZAR.Zero,
            allowances: MoneyZAR.Zero,
            grossPay: 50_000m,
            paye: 10_000m,
            uifEmployee: 177.12m,
            uifEmployer: 177.12m,
            sdl: 500m,
            pensionEmployee: MoneyZAR.Zero,
            pensionEmployer: MoneyZAR.Zero,
            medicalEmployee: MoneyZAR.Zero,
            medicalEmployer: MoneyZAR.Zero,
            etiAmount: MoneyZAR.Zero,
            etiEligible: false,
            otherDeductions: [],
            otherAdditions: [],
            deductionTotal: 10_177.12m,
            additionTotal: MoneyZAR.Zero,
            netPay: 39_822.88m,
            hoursOrdinary: 176m,
            hoursOvertime: 0m,
            taxTableVersion: "v2026.1.0",
            complianceFlags: [],
            calculationTimestamp: ts);

        result.EmployeeId.Should().Be("emp_fs_001");
        result.GrossPay.Amount.Should().Be(50_000m);
        result.DeductionTotal.Amount.Should().Be(10_177.12m);
        result.NetPay.Amount.Should().Be(39_822.88m);
        result.CalculationTimestamp.Should().Be(ts);
    }
}
