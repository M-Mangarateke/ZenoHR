// TC-PAY-010: PayrollRun state machine — state transition and invariant tests.
// REQ-HR-003, CTL-SARS-001: Verifies Draft→Calculated→Finalized→Filed lifecycle.
using FluentAssertions;
using ZenoHR.Domain.Common;
using ZenoHR.Module.Payroll.Aggregates;
using ZenoHR.Module.Payroll.Calculation;
using ZenoHR.Module.Payroll.Events;

namespace ZenoHR.Module.Payroll.Tests.Aggregates;

public sealed class PayrollRunStateMachineTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private const string TenantId = "tenant_test";
    private const string ActorId = "actor_hr";

    private static PayrollRun CreateValidDraft() =>
        PayrollRun.Create(
            id: "pr_2026_03_001",
            tenantId: TenantId,
            period: "2026-03",
            runType: PayFrequency.Monthly,
            employeeIds: ["emp_001", "emp_002"],
            ruleSetVersion: "v2026.1.0",
            initiatedBy: ActorId,
            idempotencyKey: "idem_001",
            now: Now).Value!;

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidInputs_ReturnsSuccess()
    {
        // TC-PAY-010-001
        var result = PayrollRun.Create(
            "pr_2026_03_001", TenantId, "2026-03", PayFrequency.Monthly,
            ["emp_001"], "v2026.1.0", ActorId, "idem", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PayrollRunStatus.Draft);
        result.Value.EmployeeCount.Should().Be(1);
    }

    [Fact]
    public void Create_EmptyEmployeeList_ReturnsFailure()
    {
        // TC-PAY-010-002
        var result = PayrollRun.Create(
            "pr_2026_03_001", TenantId, "2026-03", PayFrequency.Monthly,
            [], "v2026.1.0", ActorId, "idem", Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("employee");
    }

    [Fact]
    public void Create_UnknownRunType_ReturnsFailure()
    {
        // TC-PAY-010-003
        var result = PayrollRun.Create(
            "pr_2026_03_001", TenantId, "2026-03", PayFrequency.Unknown,
            ["emp_001"], "v2026.1.0", ActorId, "idem", Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_RaisesCreatedEvent()
    {
        // TC-PAY-010-004
        var run = CreateValidDraft();
        var events = run.PopDomainEvents();

        events.Should().HaveCount(1);
        events[0].Should().BeOfType<PayrollRunCreatedEvent>();
        var evt = (PayrollRunCreatedEvent)events[0];
        evt.PayrollRunId.Should().Be("pr_2026_03_001");
        evt.TenantId.Should().Be(TenantId);
        evt.ActorId.Should().Be(ActorId);
    }

    [Fact]
    public void Create_MissingTenantId_ReturnsFailure()
    {
        // TC-PAY-010-005
        var result = PayrollRun.Create(
            "pr_2026_03_001", "", "2026-03", PayFrequency.Monthly,
            ["emp_001"], "v2026.1.0", ActorId, "idem", Now);

        result.IsFailure.Should().BeTrue();
    }

    // ── MarkCalculated ────────────────────────────────────────────────────────

    [Fact]
    public void MarkCalculated_FromDraft_TransitionsToCalculated()
    {
        // TC-PAY-010-006
        var run = CreateValidDraft();
        run.PopDomainEvents(); // clear created event

        var gross = new MoneyZAR(50_000m);
        var paye = new MoneyZAR(10_000m);
        var uif = new MoneyZAR(177.12m);
        var sdl = new MoneyZAR(500m);
        var eti = new MoneyZAR(2_500m);
        var deductions = new MoneyZAR(12_000m);
        var net = new MoneyZAR(38_000m);

        var result = run.MarkCalculated(gross, paye, uif, sdl, eti, deductions, net,
            ["CTL-SARS-001:PASS"], ActorId, Now);

        result.IsSuccess.Should().BeTrue();
        run.Status.Should().Be(PayrollRunStatus.Calculated);
        run.GrossTotal.Should().Be(gross);
        run.PayeTotal.Should().Be(paye);
        run.CalculatedAt.Should().Be(Now);
    }

    [Fact]
    public void MarkCalculated_FromCalculated_ReturnsFailure()
    {
        // TC-PAY-010-007 — cannot recalculate after already calculated
        var run = CreateValidDraft();
        run.MarkCalculated(50_000m, 10_000m, 177.12m, 500m, 0m, 12_000m, 38_000m, [], ActorId, Now);

        var result = run.MarkCalculated(60_000m, 12_000m, 177.12m, 600m, 0m, 14_000m, 46_000m, [], ActorId, Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("Draft");
    }

    [Fact]
    public void MarkCalculated_RaisesCalculatedEvent()
    {
        // TC-PAY-010-008
        var run = CreateValidDraft();
        run.PopDomainEvents();

        run.MarkCalculated(50_000m, 10_000m, 177.12m, 500m, 0m, 12_000m, 38_000m,
            ["CTL-SARS-001:PASS"], ActorId, Now);

        var events = run.PopDomainEvents();
        events.Should().HaveCount(1);
        events[0].Should().BeOfType<PayrollRunCalculatedEvent>();
        var evt = (PayrollRunCalculatedEvent)events[0];
        evt.TenantId.Should().Be(TenantId);
        evt.ActorId.Should().Be(ActorId);
    }

    // ── Finalize ──────────────────────────────────────────────────────────────

    [Fact]
    public void Finalize_FromCalculated_TransitionsToFinalized()
    {
        // TC-PAY-010-009
        var run = CreateValidDraft();
        run.MarkCalculated(50_000m, 10_000m, 177.12m, 500m, 0m, 12_000m, 38_000m,
            ["CTL-SARS-001:PASS"], ActorId, Now);
        run.PopDomainEvents();

        var result = run.Finalize("sha256checksum", ActorId, Now);

        result.IsSuccess.Should().BeTrue();
        run.Status.Should().Be(PayrollRunStatus.Finalized);
        run.IsImmutable.Should().BeTrue();
        run.FinalizedBy.Should().Be(ActorId);
        run.Checksum.Should().Be("sha256checksum");
    }

    [Fact]
    public void Finalize_WithCriticalComplianceFlag_Blocks()
    {
        // TC-PAY-010-010 — CTL-SARS-001: critical flags block finalization
        var run = CreateValidDraft();
        run.MarkCalculated(50_000m, 10_000m, 177.12m, 500m, 0m, 12_000m, 38_000m,
            ["CTL-SARS-001:FAIL"], ActorId, Now); // Critical flag
        run.PopDomainEvents();

        var result = run.Finalize("sha256checksum", ActorId, Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("compliance flag");
    }

    [Fact]
    public void Finalize_WithWarnFlag_Succeeds()
    {
        // TC-PAY-010-011 — WARN flags do not block finalization
        var run = CreateValidDraft();
        run.MarkCalculated(50_000m, 10_000m, 177.12m, 500m, 0m, 12_000m, 38_000m,
            ["CTL-SARS-001:WARN"], ActorId, Now);

        var result = run.Finalize("sha256checksum", ActorId, Now);

        result.IsSuccess.Should().BeTrue();
        run.Status.Should().Be(PayrollRunStatus.Finalized);
    }

    [Fact]
    public void Finalize_FromDraft_ReturnsFailure()
    {
        // TC-PAY-010-012 — must be Calculated first
        var run = CreateValidDraft();

        var result = run.Finalize("sha256checksum", ActorId, Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("Calculated");
    }

    [Fact]
    public void Finalize_MissingChecksum_ReturnsFailure()
    {
        // TC-PAY-010-013
        var run = CreateValidDraft();
        run.MarkCalculated(50_000m, 10_000m, 177.12m, 500m, 0m, 12_000m, 38_000m, [], ActorId, Now);

        var result = run.Finalize("", ActorId, Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Finalize_RaisesFinalizedEvent()
    {
        // TC-PAY-010-014
        var run = CreateValidDraft();
        run.MarkCalculated(50_000m, 10_000m, 177.12m, 500m, 0m, 12_000m, 38_000m, [], ActorId, Now);
        run.PopDomainEvents();

        run.Finalize("sha256checksum", ActorId, Now);

        var events = run.PopDomainEvents();
        events.Should().HaveCount(1);
        events[0].Should().BeOfType<PayrollRunFinalizedEvent>();
    }

    // ── MarkFiled ─────────────────────────────────────────────────────────────

    [Fact]
    public void MarkFiled_FromFinalized_TransitionsToFiled()
    {
        // TC-PAY-010-015
        var run = CreateValidDraft();
        run.MarkCalculated(50_000m, 10_000m, 177.12m, 500m, 0m, 12_000m, 38_000m, [], ActorId, Now);
        run.Finalize("sha256", ActorId, Now);
        run.PopDomainEvents();

        var result = run.MarkFiled(ActorId, Now);

        result.IsSuccess.Should().BeTrue();
        run.Status.Should().Be(PayrollRunStatus.Filed);
        run.FiledAt.Should().Be(Now);
        run.IsImmutable.Should().BeTrue();
    }

    [Fact]
    public void MarkFiled_FromDraft_ReturnsFailure()
    {
        // TC-PAY-010-016
        var run = CreateValidDraft();

        var result = run.MarkFiled(ActorId, Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("Finalized");
    }

    [Fact]
    public void MarkFiled_RaisesFiledEvent()
    {
        // TC-PAY-010-017
        var run = CreateValidDraft();
        run.MarkCalculated(50_000m, 10_000m, 177.12m, 500m, 0m, 12_000m, 38_000m, [], ActorId, Now);
        run.Finalize("sha256", ActorId, Now);
        run.PopDomainEvents();

        run.MarkFiled(ActorId, Now);

        var events = run.PopDomainEvents();
        events.Should().HaveCount(1);
        events[0].Should().BeOfType<PayrollRunFiledEvent>();
        var evt = (PayrollRunFiledEvent)events[0];
        evt.PayrollRunId.Should().Be("pr_2026_03_001");
        evt.TenantId.Should().Be(TenantId);
    }

    // ── Immutability guard ────────────────────────────────────────────────────

    [Fact]
    public void IsImmutable_Draft_ReturnsFalse()
    {
        // TC-PAY-010-018
        var run = CreateValidDraft();
        run.IsImmutable.Should().BeFalse();
    }

    [Fact]
    public void IsImmutable_Finalized_ReturnsTrue()
    {
        // TC-PAY-010-019
        var run = CreateValidDraft();
        run.MarkCalculated(50_000m, 10_000m, 177.12m, 500m, 0m, 12_000m, 38_000m, [], ActorId, Now);
        run.Finalize("sha256", ActorId, Now);

        run.IsImmutable.Should().BeTrue();
    }

    // ── PopDomainEvents clears the list ───────────────────────────────────────

    [Fact]
    public void PopDomainEvents_ClearsInternalList()
    {
        // TC-PAY-010-020
        var run = CreateValidDraft();

        var first = run.PopDomainEvents();
        var second = run.PopDomainEvents();

        first.Should().HaveCount(1);
        second.Should().BeEmpty();
    }
}
