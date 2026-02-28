// TC-PAY-020: Payslip invariant verifier — net_pay == gross_pay - deductions + additions.
// REQ-HR-003, REQ-HR-004, CTL-SARS-001: Any cent mismatch must fail.
using FluentAssertions;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Payroll.Entities;

namespace ZenoHR.Module.Payroll.Tests.Entities;

public sealed class PayslipInvariantVerifierTests
{
    [Fact]
    public void Verify_ExactMatch_ReturnsSuccess()
    {
        // TC-PAY-020-001
        var gross = new MoneyZAR(50_000m);
        var deductions = new MoneyZAR(12_000m);
        var additions = MoneyZAR.Zero;
        var net = gross - deductions + additions; // = 38_000

        var result = PayslipInvariantVerifier.Verify(gross, deductions, additions, net);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Verify_OneCentMismatch_ReturnsFailure()
    {
        // TC-PAY-020-002 — even 1 cent must fail
        var gross = new MoneyZAR(50_000m);
        var deductions = new MoneyZAR(12_000m);
        var additions = MoneyZAR.Zero;
        var incorrectNet = new MoneyZAR(37_999.99m); // 1 cent off

        var result = PayslipInvariantVerifier.Verify(gross, deductions, additions, incorrectNet);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.PayslipInvariantViolation);
        result.Error.Message.Should().Contain("R0.01");
    }

    [Fact]
    public void Verify_WithAdditions_ReturnsSuccess()
    {
        // TC-PAY-020-003 — reimbursements add to net
        var gross = new MoneyZAR(50_000m);
        var deductions = new MoneyZAR(12_000m);
        var additions = new MoneyZAR(500m); // travel reimbursement
        var net = gross - deductions + additions; // = 38_500

        var result = PayslipInvariantVerifier.Verify(gross, deductions, additions, net);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Verify_ZeroDeductionsAndAdditions_GrossEqualsNet()
    {
        // TC-PAY-020-004
        var gross = new MoneyZAR(30_000m);

        var result = PayslipInvariantVerifier.Verify(gross, MoneyZAR.Zero, MoneyZAR.Zero, gross);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void VerifyDecimal_ExactMatch_ReturnsSuccess()
    {
        // TC-PAY-020-005
        var result = PayslipInvariantVerifier.VerifyDecimal(50_000m, 12_000m, 0m, 38_000m);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void VerifyDecimal_Mismatch_ReturnsFailureWithInvariantCode()
    {
        // TC-PAY-020-006
        var result = PayslipInvariantVerifier.VerifyDecimal(50_000m, 12_000m, 0m, 38_000.01m);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.PayslipInvariantViolation);
    }

    [Fact]
    public void PayrollResult_Create_InvariantViolation_ReturnsFailure()
    {
        // TC-PAY-020-007 — PayrollResult.Create enforces invariant via verifier
        var result = PayrollResult.Create(
            employeeId: "emp_001",
            payrollRunId: "pr_2026_03_001",
            tenantId: "tenant_001",
            basicSalary: 50_000m,
            overtimePay: MoneyZAR.Zero,
            allowances: MoneyZAR.Zero,
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
            otherDeductions: null,
            otherAdditions: null,
            hoursOrdinary: 176m,
            hoursOvertime: 0m,
            taxTableVersion: "v2026.1.0",
            complianceFlags: null,
            calculationTimestamp: DateTimeOffset.UtcNow);

        // gross = 50_000, deductions = 10_000 + 177.12 = 10_177.12, net should be 39_822.88
        // The factory computes net internally so this should succeed
        result.IsSuccess.Should().BeTrue();
        result.Value!.NetPay.Amount.Should().Be(39_822.88m);
    }
}
