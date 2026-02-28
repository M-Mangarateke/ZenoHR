// TC-PAY-030: PayrollAdjustment entity — create, validation, reconstitution.
// REQ-HR-003, CTL-SARS-001: Post-finalization adjustments are immutable and append-only.
using FluentAssertions;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Payroll.Entities;

namespace ZenoHR.Module.Payroll.Tests.Entities;

public sealed class PayrollAdjustmentTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Result<PayrollAdjustment> ValidCreate(
        string? adjustmentId = "adj_001",
        string? tenantId = "tenant_001",
        string? payrollRunId = "pr_001",
        string? employeeId = "emp_001",
        PayrollAdjustmentType type = PayrollAdjustmentType.Correction,
        string? reason = "PAYE under-deduction",
        decimal amount = 500m,
        string[]? affectedFields = null,
        string? createdBy = "actor_hrm",
        string? approvedBy = null)
    {
        return PayrollAdjustment.Create(
            adjustmentId!, tenantId!, payrollRunId!, employeeId!,
            type, reason!, new MoneyZAR(amount),
            affectedFields ?? ["paye_zar", "net_pay_zar"],
            createdBy!, approvedBy, DateTimeOffset.UtcNow);
    }

    // ── Success paths ─────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithAllValidInputs_ReturnsSuccess()
    {
        // TC-PAY-030-001
        var result = ValidCreate();

        result.IsSuccess.Should().BeTrue();
        result.Value!.AdjustmentId.Should().Be("adj_001");
        result.Value.TenantId.Should().Be("tenant_001");
        result.Value.PayrollRunId.Should().Be("pr_001");
        result.Value.EmployeeId.Should().Be("emp_001");
        result.Value.AdjustmentType.Should().Be(PayrollAdjustmentType.Correction);
        result.Value.Reason.Should().Be("PAYE under-deduction");
        result.Value.Amount.Amount.Should().Be(500m);
        result.Value.AffectedFields.Should().BeEquivalentTo(["paye_zar", "net_pay_zar"]);
        result.Value.CreatedBy.Should().Be("actor_hrm");
        result.Value.ApprovedBy.Should().BeNull();
        result.Value.SchemaVersion.Should().Be("1.0");
    }

    [Fact]
    public void Create_NegativeAmount_ReturnsSuccess()
    {
        // TC-PAY-030-002 — reversals use negative amounts (overpayment recovery)
        var result = ValidCreate(amount: -1_500m, type: PayrollAdjustmentType.Reversal,
            reason: "Erroneous duplicate payment reversed");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Amount.Amount.Should().Be(-1_500m);
    }

    [Fact]
    public void Create_WithApprovedBy_SetsApprovedBy()
    {
        // TC-PAY-030-003
        var result = ValidCreate(approvedBy: "actor_director");

        result.IsSuccess.Should().BeTrue();
        result.Value!.ApprovedBy.Should().Be("actor_director");
    }

    [Fact]
    public void Create_SupplementaryType_ReturnsSuccess()
    {
        // TC-PAY-030-004 — supplementary payment for missed overtime
        var result = ValidCreate(
            type: PayrollAdjustmentType.Supplementary,
            reason: "Missed overtime — March 2026",
            affectedFields: ["overtime_pay_zar", "net_pay_zar"]);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AdjustmentType.Should().Be(PayrollAdjustmentType.Supplementary);
    }

    [Fact]
    public void Create_CreatedAtIsSet_FromNow()
    {
        // TC-PAY-030-005
        var before = DateTimeOffset.UtcNow;
        var result = ValidCreate();
        var after = DateTimeOffset.UtcNow;

        result.IsSuccess.Should().BeTrue();
        result.Value!.CreatedAt.Should().BeOnOrAfter(before);
        result.Value!.CreatedAt.Should().BeOnOrBefore(after);
    }

    // ── Validation failures ───────────────────────────────────────────────────

    [Fact]
    public void Create_EmptyAdjustmentId_ReturnsValidationFailure()
    {
        // TC-PAY-030-010
        var result = ValidCreate(adjustmentId: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Contain("AdjustmentId");
    }

    [Fact]
    public void Create_EmptyTenantId_ReturnsValidationFailure()
    {
        // TC-PAY-030-011
        var result = ValidCreate(tenantId: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Contain("TenantId");
    }

    [Fact]
    public void Create_EmptyPayrollRunId_ReturnsValidationFailure()
    {
        // TC-PAY-030-012
        var result = ValidCreate(payrollRunId: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Contain("PayrollRunId");
    }

    [Fact]
    public void Create_EmptyEmployeeId_ReturnsValidationFailure()
    {
        // TC-PAY-030-013
        var result = ValidCreate(employeeId: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Contain("EmployeeId");
    }

    [Fact]
    public void Create_UnknownAdjustmentType_ReturnsValidationFailure()
    {
        // TC-PAY-030-014 — Unknown=0 is sentinel; must not be used in Create
        var result = ValidCreate(type: PayrollAdjustmentType.Unknown);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Contain("AdjustmentType");
    }

    [Fact]
    public void Create_EmptyReason_ReturnsValidationFailure()
    {
        // TC-PAY-030-015
        var result = ValidCreate(reason: "  ");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Contain("Reason");
    }

    [Fact]
    public void Create_EmptyAffectedFields_ReturnsValidationFailure()
    {
        // TC-PAY-030-016 — at least one affected field must be named
        var result = ValidCreate(affectedFields: []);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Contain("AffectedFields");
    }

    [Fact]
    public void Create_EmptyCreatedBy_ReturnsValidationFailure()
    {
        // TC-PAY-030-017
        var result = ValidCreate(createdBy: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Contain("CreatedBy");
    }

    // ── Reconstitution ────────────────────────────────────────────────────────

    [Fact]
    public void Reconstitute_SetsAllProperties_Correctly()
    {
        // TC-PAY-030-020 — read-path from Firestore bypasses validation
        var ts = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

        var adj = PayrollAdjustment.Reconstitute(
            adjustmentId: "adj_fs_001",
            tenantId: "tenant_fs",
            payrollRunId: "pr_fs_001",
            employeeId: "emp_fs_001",
            adjustmentType: PayrollAdjustmentType.Reversal,
            reason: "Duplicate payment reversal",
            amount: new MoneyZAR(-2_000m),
            affectedFields: ["net_pay_zar"],
            createdBy: "actor_dir",
            approvedBy: "actor_hrm",
            createdAt: ts);

        adj.AdjustmentId.Should().Be("adj_fs_001");
        adj.TenantId.Should().Be("tenant_fs");
        adj.PayrollRunId.Should().Be("pr_fs_001");
        adj.EmployeeId.Should().Be("emp_fs_001");
        adj.AdjustmentType.Should().Be(PayrollAdjustmentType.Reversal);
        adj.Reason.Should().Be("Duplicate payment reversal");
        adj.Amount.Amount.Should().Be(-2_000m);
        adj.AffectedFields.Should().ContainSingle("net_pay_zar");
        adj.CreatedBy.Should().Be("actor_dir");
        adj.ApprovedBy.Should().Be("actor_hrm");
        adj.CreatedAt.Should().Be(ts);
    }
}
