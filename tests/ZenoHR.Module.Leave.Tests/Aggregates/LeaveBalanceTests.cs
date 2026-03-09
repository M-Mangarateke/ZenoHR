// TC-LEAVE-002: LeaveBalance aggregate — create, accrue, consume, reverse.
// REQ-HR-002, CTL-BCEA-003: Balance cannot go negative without allowNegativeBalance flag.
using FluentAssertions;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Leave.Aggregates;

namespace ZenoHR.Module.Leave.Tests.Aggregates;

public sealed class LeaveBalanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 3, 1);

    private static Result<LeaveBalance> ValidCreate(
        string? balanceId = "lb_001",
        string? tenantId = "tenant_001",
        string? empId = "emp_001",
        LeaveType type = LeaveType.Annual,
        string? cycleId = "2026",
        string? policyVersion = "v2026.1.0")
        => LeaveBalance.Create(balanceId!, tenantId!, empId!, type, cycleId!, policyVersion!, Now);

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidInput_ReturnsZeroBalance()
    {
        // TC-LEAVE-002-001
        var result = ValidCreate();

        result.IsSuccess.Should().BeTrue();
        result.Value!.AccruedHours.Should().Be(0);
        result.Value.ConsumedHours.Should().Be(0);
        result.Value.AdjustmentHours.Should().Be(0);
        result.Value.AvailableHours.Should().Be(0);
    }

    [Fact]
    public void Create_EmptyBalanceId_ReturnsFailure()
    {
        // TC-LEAVE-002-002
        var result = ValidCreate(balanceId: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    [Fact]
    public void Create_UnknownLeaveType_ReturnsFailure()
    {
        // TC-LEAVE-002-003
        var result = ValidCreate(type: LeaveType.Unknown);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.InvalidLeaveType);
    }

    [Fact]
    public void Create_EmptyCycleId_ReturnsFailure()
    {
        // TC-LEAVE-002-004
        var result = ValidCreate(cycleId: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    // ── PostAccrual ───────────────────────────────────────────────────────────

    [Fact]
    public void PostAccrual_PositiveHours_IncreasesAccruedHours()
    {
        // TC-LEAVE-002-010
        var bal = ValidCreate().Value!;

        var result = bal.PostAccrual("le_001", 16m, Today, "MONTHLY_ACCRUAL", "v2026.1.0", Now);

        result.IsSuccess.Should().BeTrue();
        bal.AccruedHours.Should().Be(16m);
        bal.AvailableHours.Should().Be(16m);
    }

    [Fact]
    public void PostAccrual_AddsLedgerEntry()
    {
        // TC-LEAVE-002-011 — pending entries must be populated for persistence
        var bal = ValidCreate().Value!;
        bal.PostAccrual("le_001", 16m, Today, "MONTHLY_ACCRUAL", "v2026.1.0", Now);

        var entries = bal.PopPendingEntries();
        entries.Should().HaveCount(1);
        entries[0].EntryType.Should().Be(AccrualEntryType.Accrual);
        entries[0].Hours.Should().Be(16m);
    }

    [Fact]
    public void PostAccrual_ZeroHours_ReturnsFailure()
    {
        // TC-LEAVE-002-012
        var bal = ValidCreate().Value!;

        var result = bal.PostAccrual("le_001", 0m, Today, "MONTHLY_ACCRUAL", "v2026.1.0", Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValueOutOfRange);
    }

    [Fact]
    public void PostAccrual_NegativeHours_ReturnsFailure()
    {
        // TC-LEAVE-002-013 — AccrualLedgerEntry.Create enforces this
        var bal = ValidCreate().Value!;

        var result = bal.PostAccrual("le_001", -8m, Today, "MONTHLY_ACCRUAL", "v2026.1.0", Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void PostAccrual_MultipleAccruals_AccumulatesCorrectly()
    {
        // TC-LEAVE-002-014
        var bal = ValidCreate().Value!;
        bal.PostAccrual("le_001", 16m, Today, "MONTHLY_ACCRUAL", "v2026.1.0", Now);
        bal.PostAccrual("le_002", 16m, Today.AddMonths(1), "MONTHLY_ACCRUAL", "v2026.1.0", Now.AddMonths(1));

        bal.AccruedHours.Should().Be(32m);
        bal.AvailableHours.Should().Be(32m);
    }

    // ── ConsumeHours ──────────────────────────────────────────────────────────

    [Fact]
    public void ConsumeHours_SufficientBalance_Succeeds()
    {
        // TC-LEAVE-002-020
        var bal = ValidCreate().Value!;
        bal.PostAccrual("le_001", 40m, Today, "MONTHLY_ACCRUAL", "v2026.1.0", Now);
        bal.PopPendingEntries();

        var result = bal.ConsumeHours("le_002", "lr_001", 24m, Today, "v2026.1.0", "mgr_001", Now);

        result.IsSuccess.Should().BeTrue();
        bal.ConsumedHours.Should().Be(24m);
        bal.AvailableHours.Should().Be(16m);
    }

    [Fact]
    public void ConsumeHours_AddsConsumptionLedgerEntry()
    {
        // TC-LEAVE-002-021
        var bal = ValidCreate().Value!;
        bal.PostAccrual("le_001", 40m, Today, "MONTHLY_ACCRUAL", "v2026.1.0", Now);
        bal.PopPendingEntries();

        bal.ConsumeHours("le_002", "lr_001", 24m, Today, "v2026.1.0", "mgr_001", Now);
        var entries = bal.PopPendingEntries();

        entries.Should().HaveCount(1);
        entries[0].EntryType.Should().Be(AccrualEntryType.Consumption);
        entries[0].Hours.Should().Be(-24m);
    }

    [Fact]
    public void ConsumeHours_InsufficientBalance_ReturnsFailure()
    {
        // TC-LEAVE-002-022 — CTL-BCEA-003: balance cannot go negative
        var bal = ValidCreate().Value!;
        bal.PostAccrual("le_001", 8m, Today, "MONTHLY_ACCRUAL", "v2026.1.0", Now);
        bal.PopPendingEntries();

        var result = bal.ConsumeHours("le_002", "lr_001", 24m, Today, "v2026.1.0", "mgr_001", Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.InsufficientLeaveBalance);
    }

    [Fact]
    public void ConsumeHours_AllowNegativeBalance_Succeeds()
    {
        // TC-LEAVE-002-023 — policy exception path
        var bal = ValidCreate().Value!;
        bal.PostAccrual("le_001", 8m, Today, "MONTHLY_ACCRUAL", "v2026.1.0", Now);
        bal.PopPendingEntries();

        var result = bal.ConsumeHours("le_002", "lr_001", 24m, Today, "v2026.1.0", "mgr_001", Now, allowNegativeBalance: true);

        result.IsSuccess.Should().BeTrue();
        bal.AvailableHours.Should().Be(-16m);
    }

    [Fact]
    public void ConsumeHours_ZeroHours_ReturnsFailure()
    {
        // TC-LEAVE-002-024
        var bal = ValidCreate().Value!;

        var result = bal.ConsumeHours("le_001", "lr_001", 0m, Today, "v2026.1.0", "mgr_001", Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValueOutOfRange);
    }

    // ── ReverseConsumption ────────────────────────────────────────────────────

    [Fact]
    public void ReverseConsumption_AfterConsume_RestoresBalance()
    {
        // TC-LEAVE-002-030
        var bal = ValidCreate().Value!;
        bal.PostAccrual("le_001", 40m, Today, "MONTHLY_ACCRUAL", "v2026.1.0", Now);
        bal.PopPendingEntries();
        bal.ConsumeHours("le_002", "lr_001", 24m, Today, "v2026.1.0", "mgr_001", Now);
        bal.PopPendingEntries();

        var result = bal.ReverseConsumption("le_003", "lr_001", 24m, Today, "v2026.1.0", "mgr_001", Now);

        result.IsSuccess.Should().BeTrue();
        bal.ConsumedHours.Should().Be(0m);     // max(0, 24-24)
        bal.AdjustmentHours.Should().Be(0m);   // reversal reduces ConsumedHours only; no AdjustmentHours change
        bal.AvailableHours.Should().Be(40m);   // AccruedHours(40) - ConsumedHours(0) + AdjustmentHours(0) = 40
    }

    [Fact]
    public void ReverseConsumption_ZeroHours_ReturnsFailure()
    {
        // TC-LEAVE-002-031
        var bal = ValidCreate().Value!;

        var result = bal.ReverseConsumption("le_001", "lr_001", 0m, Today, "v2026.1.0", "mgr_001", Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValueOutOfRange);
    }

    // ── PopDomainEvents ───────────────────────────────────────────────────────

    [Fact]
    public void PopDomainEvents_ReturnsEmptyForNewBalance()
    {
        // TC-LEAVE-002-040
        var bal = ValidCreate().Value!;

        var events = bal.PopDomainEvents();
        events.Should().BeEmpty();
    }

    // ── Reconstitution ────────────────────────────────────────────────────────

    [Fact]
    public void Reconstitute_SetsAllFields()
    {
        // TC-LEAVE-002-050
        var bal = LeaveBalance.Reconstitute(
            "lb_fs_001", "tenant_fs", "emp_fs_001",
            LeaveType.Annual, "2026",
            accruedHours: 80m, consumedHours: 24m, adjustmentHours: 0m,
            "v2026.1.0", Today, Now, Now.AddHours(1));

        bal.AccruedHours.Should().Be(80m);
        bal.ConsumedHours.Should().Be(24m);
        bal.AvailableHours.Should().Be(56m);
        bal.TenantId.Should().Be("tenant_fs");
    }
}
