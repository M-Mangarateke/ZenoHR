// TC-LEAVE-003: AccrualLedgerEntry — immutable append-only ledger validation.
// REQ-HR-002, CTL-BCEA-003: Accrual/consumption type sign rules enforced.
using FluentAssertions;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Leave.Aggregates;

namespace ZenoHR.Module.Leave.Tests.Aggregates;

public sealed class AccrualLedgerEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 3, 1);

    private static Result<AccrualLedgerEntry> ValidCreate(
        string? ledgerId = "le_001",
        string? balanceId = "lb_001",
        string? tenantId = "tenant_001",
        string? empId = "emp_001",
        AccrualEntryType entryType = AccrualEntryType.Accrual,
        decimal hours = 16m,
        string? reasonCode = "MONTHLY_ACCRUAL",
        string? policyVersion = "v2026.1.0",
        string? postedBy = "system")
        => AccrualLedgerEntry.Create(
            ledgerId!, balanceId!, tenantId!, empId!,
            entryType, hours, Today, reasonCode!, null, policyVersion!, postedBy!, Now);

    // ── Success paths ─────────────────────────────────────────────────────────

    [Fact]
    public void Create_AccrualPositiveHours_ReturnsSuccess()
    {
        // TC-LEAVE-003-001
        var result = ValidCreate(entryType: AccrualEntryType.Accrual, hours: 16m);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Hours.Should().Be(16m);
        result.Value.EntryType.Should().Be(AccrualEntryType.Accrual);
    }

    [Fact]
    public void Create_ConsumptionNegativeHours_ReturnsSuccess()
    {
        // TC-LEAVE-003-002 — consumption must use negative hours
        var result = ValidCreate(entryType: AccrualEntryType.Consumption, hours: -24m,
            reasonCode: "leave_taken");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Hours.Should().Be(-24m);
    }

    [Fact]
    public void Create_CarryoverPositiveHours_ReturnsSuccess()
    {
        // TC-LEAVE-003-003
        var result = ValidCreate(entryType: AccrualEntryType.Carryover, hours: 40m,
            reasonCode: "ANNUAL_CARRYOVER");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_ForfeitureNegativeHours_ReturnsSuccess()
    {
        // TC-LEAVE-003-004
        var result = ValidCreate(entryType: AccrualEntryType.Forfeiture, hours: -8m,
            reasonCode: "FORFEITURE");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_AdjustmentPositive_ReturnsSuccess()
    {
        // TC-LEAVE-003-005 — adjustments can be positive or negative
        var result = ValidCreate(entryType: AccrualEntryType.Adjustment, hours: 8m,
            reasonCode: "leave_reversal");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_AdjustmentNegative_ReturnsSuccess()
    {
        // TC-LEAVE-003-006
        var result = ValidCreate(entryType: AccrualEntryType.Adjustment, hours: -8m,
            reasonCode: "leave_clawback");

        result.IsSuccess.Should().BeTrue();
    }

    // ── Sign enforcement ──────────────────────────────────────────────────────

    [Fact]
    public void Create_AccrualNegativeHours_ReturnsFailure()
    {
        // TC-LEAVE-003-010 — accruals must be positive
        var result = ValidCreate(entryType: AccrualEntryType.Accrual, hours: -8m);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValueOutOfRange);
    }

    [Fact]
    public void Create_ConsumptionPositiveHours_ReturnsFailure()
    {
        // TC-LEAVE-003-011 — consumptions must be negative
        var result = ValidCreate(entryType: AccrualEntryType.Consumption, hours: 8m,
            reasonCode: "leave_taken");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValueOutOfRange);
    }

    [Fact]
    public void Create_ZeroHours_ReturnsFailure()
    {
        // TC-LEAVE-003-012
        var result = ValidCreate(hours: 0m);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public void Create_UnknownEntryType_ReturnsFailure()
    {
        // TC-LEAVE-003-020
        var result = ValidCreate(entryType: AccrualEntryType.Unknown);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    [Fact]
    public void Create_EmptyLedgerId_ReturnsFailure()
    {
        // TC-LEAVE-003-021
        var result = ValidCreate(ledgerId: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    [Fact]
    public void Create_EmptyPolicyVersion_ReturnsFailure()
    {
        // TC-LEAVE-003-022
        var result = ValidCreate(policyVersion: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }
}
