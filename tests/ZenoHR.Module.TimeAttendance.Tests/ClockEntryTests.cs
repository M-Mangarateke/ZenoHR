// TC-OPS-010: ClockEntry — clock-in/out state machine, flag, link.
// REQ-OPS-003, CTL-BCEA-001: Employee self-service time recording.
using FluentAssertions;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Module.TimeAttendance.Tests;

public sealed class ClockEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

    private static Result<ClockEntry> ValidClockIn(
        string? entryId = "ce_001",
        string? tenantId = "tenant_001",
        string? empId = "emp_001",
        ClockEntrySource source = ClockEntrySource.EmployeeSelf)
        => ClockEntry.ClockIn(entryId!, tenantId!, empId!, source, Now);

    // ── ClockIn factory ───────────────────────────────────────────────────────

    [Fact]
    public void ClockIn_ValidInput_ReturnsOpenEntry()
    {
        // TC-OPS-010-001
        var result = ValidClockIn();

        result.IsSuccess.Should().BeTrue();
        result.Value!.EntryId.Should().Be("ce_001");
        result.Value.Status.Should().Be(ClockEntryStatus.Open);
        result.Value.ClockInAt.Should().Be(Now);
        result.Value.ClockOutAt.Should().BeNull();
        result.Value.CalculatedHours.Should().BeNull();
    }

    [Fact]
    public void ClockIn_DateSetFromNow()
    {
        // TC-OPS-010-002 — Date is derived from ClockInAt, never accepted from client
        var result = ValidClockIn();

        result.Value!.Date.Should().Be(DateOnly.FromDateTime(Now.UtcDateTime));
    }

    [Fact]
    public void ClockIn_EmptyEntryId_ReturnsValidationFailure()
    {
        // TC-OPS-010-003
        var result = ValidClockIn(entryId: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    [Fact]
    public void ClockIn_EmptyTenantId_ReturnsValidationFailure()
    {
        // TC-OPS-010-004
        var result = ValidClockIn(tenantId: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    [Fact]
    public void ClockIn_EmptyEmployeeId_ReturnsValidationFailure()
    {
        // TC-OPS-010-005
        var result = ValidClockIn(empId: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    [Fact]
    public void ClockIn_UnknownSource_ReturnsValidationFailure()
    {
        // TC-OPS-010-006
        var result = ValidClockIn(source: ClockEntrySource.Unknown);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    [Fact]
    public void ClockIn_ManagerEntrySource_Succeeds()
    {
        // TC-OPS-010-007 — manager-entered entries are valid
        var result = ValidClockIn(source: ClockEntrySource.ManagerEntry);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Source.Should().Be(ClockEntrySource.ManagerEntry);
    }

    // ── ClockOut ──────────────────────────────────────────────────────────────

    [Fact]
    public void ClockOut_AfterClockIn_ComputesHours()
    {
        // TC-OPS-010-010
        var entry = ValidClockIn().Value!;
        var clockOut = Now.AddHours(8);

        var result = entry.ClockOut(clockOut, Now.AddHours(8));

        result.IsSuccess.Should().BeTrue();
        entry.ClockOutAt.Should().Be(clockOut);
        entry.CalculatedHours.Should().BeApproximately(8m, 0.001m);
        entry.Status.Should().Be(ClockEntryStatus.Completed);
    }

    [Fact]
    public void ClockOut_BeforeClockIn_ReturnsFailure()
    {
        // TC-OPS-010-011 — server must reject illogical timestamps
        var entry = ValidClockIn().Value!;

        var result = entry.ClockOut(Now.AddMinutes(-1), Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValueOutOfRange);
    }

    [Fact]
    public void ClockOut_SameTimeAsClockIn_ReturnsFailure()
    {
        // TC-OPS-010-012
        var entry = ValidClockIn().Value!;

        var result = entry.ClockOut(Now, Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValueOutOfRange);
    }

    [Fact]
    public void ClockOut_WhenNotOpen_ReturnsFailure()
    {
        // TC-OPS-010-013 — cannot clock out twice
        var entry = ValidClockIn().Value!;
        entry.ClockOut(Now.AddHours(8), Now.AddHours(8));

        var result = entry.ClockOut(Now.AddHours(9), Now.AddHours(9));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    // ── Flag ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Flag_WithNote_SetsFlaggedStatus()
    {
        // TC-OPS-010-020
        var entry = ValidClockIn().Value!;

        var result = entry.Flag("Suspected absence — no clocked-out", Now.AddHours(10));

        result.IsSuccess.Should().BeTrue();
        entry.Status.Should().Be(ClockEntryStatus.Flagged);
        entry.FlagNote.Should().Be("Suspected absence — no clocked-out");
    }

    [Fact]
    public void Flag_EmptyNote_ReturnsFailure()
    {
        // TC-OPS-010-021
        var entry = ValidClockIn().Value!;

        var result = entry.Flag("", Now.AddHours(10));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    // ── LinkToTimeEntry ───────────────────────────────────────────────────────

    [Fact]
    public void LinkToTimeEntry_SetsLinkedId()
    {
        // TC-OPS-010-030
        var entry = ValidClockIn().Value!;
        entry.ClockOut(Now.AddHours(8), Now.AddHours(8));

        entry.LinkToTimeEntry("te_001", Now.AddDays(7));

        entry.LinkedTimeEntryId.Should().Be("te_001");
    }

    // ── Reconstitution ────────────────────────────────────────────────────────

    [Fact]
    public void Reconstitute_SetsAllProperties()
    {
        // TC-OPS-010-040
        var clockOut = Now.AddHours(8);

        var entry = ClockEntry.Reconstitute(
            "ce_fs_001", "tenant_fs", "emp_fs_001",
            Now, clockOut, 8m,
            DateOnly.FromDateTime(Now.UtcDateTime),
            ClockEntrySource.EmployeeSelf,
            ClockEntryStatus.Completed,
            null, "te_001",
            Now, clockOut);

        entry.EntryId.Should().Be("ce_fs_001");
        entry.Status.Should().Be(ClockEntryStatus.Completed);
        entry.CalculatedHours.Should().Be(8m);
        entry.LinkedTimeEntryId.Should().Be("te_001");
    }
}
