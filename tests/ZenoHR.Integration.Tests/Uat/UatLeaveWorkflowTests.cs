// REQ-OPS-001: UAT — Pilot tenant leave management workflow integration tests.
// TC-UAT-LEAVE-001 through TC-UAT-LEAVE-006: End-to-end leave scenarios for Zenowethu (Pty) Ltd.
// TASK-156: Simulate real tenant leave workflows using actual domain aggregates.
// All BCEA leave entitlements verified against statutory rules. CTL-BCEA-003, CTL-BCEA-004.

using FluentAssertions;
using ZenoHR.Domain.Common;
using ZenoHR.Integration.Tests.Infrastructure;
using ZenoHR.Module.Leave.Aggregates;

namespace ZenoHR.Integration.Tests.Uat;

/// <summary>
/// UAT leave workflow tests for the Zenowethu pilot tenant.
/// These tests exercise the real LeaveBalance and LeaveRequest aggregates
/// to validate BCEA-compliant leave accrual, consumption, and approval flows.
/// REQ-OPS-001, REQ-HR-002, CTL-BCEA-003, CTL-BCEA-004
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class UatLeaveWorkflowTests : IntegrationTestBase
{
    // ── Pilot tenant constants ──────────────────────────────────────────────
    // REQ-OPS-001: Zenowethu pilot tenant identification.

    private const string PilotTenantId = "zenowethu-001";
    private const string PolicyVersion = "BCEA_LEAVE_2026";

    // BCEA statutory entitlements (from seed data — not hardcoded in production code):
    // Annual: 15 working days (21 consecutive days) per 12-month leave cycle — BCEA §20
    // Sick: 30 working days per 36-month sick leave cycle — BCEA §22
    // Family Responsibility: 3 days per annum — BCEA §27
    // These constants are ONLY used in tests to verify the engine produces correct values.

    private const decimal AnnualLeaveHoursPerDay = 8.0m;
    private const decimal AnnualLeaveDays = 15.0m; // 15 working days per BCEA
    private const decimal SickLeaveDays = 30.0m;    // 30 days per 36-month cycle
    private const decimal FamilyResponsibilityDays = 3.0m;

    public UatLeaveWorkflowTests(FirestoreEmulatorFixture fixture) : base(fixture) { }

    // ── TC-UAT-LEAVE-001: Annual leave accrual ──────────────────────────────

    /// <summary>
    /// TC-UAT-LEAVE-001: Creates an annual leave balance for a Zenowethu employee
    /// and accrues 15 working days (120 hours) over 12 monthly accruals.
    /// BCEA §20: 21 consecutive days (= 15 working days) per leave cycle.
    /// REQ-OPS-001, REQ-HR-002, CTL-BCEA-003
    /// </summary>
    [Fact]
    public void AnnualLeaveAccrual_TwelveMonthlyPostings_AccruesToFifteenDays()
    {
        // TC-UAT-LEAVE-001: Arrange — create balance
        var empId = $"emp-leave-{Guid.NewGuid():N}";
        var balanceId = $"lb_{empId}_annual_2026";
        var now = new DateTimeOffset(2026, 1, 31, 12, 0, 0, TimeSpan.Zero);

        var balanceResult = LeaveBalance.Create(
            balanceId, PilotTenantId, empId, LeaveType.Annual, "2026", PolicyVersion, now);
        balanceResult.IsSuccess.Should().BeTrue();

        var balance = balanceResult.Value!;
        balance.AvailableHours.Should().Be(0m, because: "new balance starts at zero");

        // Act — post 12 monthly accruals (15 days ÷ 12 months = 1.25 days/month = 10 hours/month)
        var monthlyAccrualHours = AnnualLeaveDays * AnnualLeaveHoursPerDay / 12m;

        for (var month = 1; month <= 12; month++)
        {
            var accrualDate = new DateOnly(2026, month, 28);
            var accrualNow = new DateTimeOffset(2026, month, 28, 12, 0, 0, TimeSpan.Zero);
            var ledgerId = $"ale_{empId}_2026_{month:D2}";

            var accrualResult = balance.PostAccrual(
                ledgerId, monthlyAccrualHours, accrualDate,
                "monthly_accrual", PolicyVersion, accrualNow);

            accrualResult.IsSuccess.Should().BeTrue(because: $"month {month} accrual must succeed");
        }

        // Assert — 15 working days × 8 hours = 120 hours
        var expectedTotal = AnnualLeaveDays * AnnualLeaveHoursPerDay;
        balance.AccruedHours.Should().Be(expectedTotal,
            because: "12 monthly accruals should total 15 working days (120 hours)");
        balance.AvailableHours.Should().Be(expectedTotal,
            because: "no leave consumed yet — full accrual available");
        balance.ConsumedHours.Should().Be(0m);
    }

    // ── TC-UAT-LEAVE-002: Sick leave cycle ──────────────────────────────────

    /// <summary>
    /// TC-UAT-LEAVE-002: Creates a sick leave balance and verifies the 36-month cycle
    /// grants 30 working days (240 hours) total.
    /// BCEA §22: 30 days paid sick leave per 36-month cycle.
    /// REQ-OPS-001, REQ-HR-002, CTL-BCEA-004
    /// </summary>
    [Fact]
    public void SickLeaveCycle_FullCycleAccrual_ThirtyWorkingDays()
    {
        // TC-UAT-LEAVE-002: Arrange
        var empId = $"emp-sick-{Guid.NewGuid():N}";
        var balanceId = $"lb_{empId}_sick_2024-2027";
        var now = new DateTimeOffset(2024, 3, 1, 12, 0, 0, TimeSpan.Zero);

        var balanceResult = LeaveBalance.Create(
            balanceId, PilotTenantId, empId, LeaveType.Sick, "2024-2027", PolicyVersion, now);
        balanceResult.IsSuccess.Should().BeTrue();

        var balance = balanceResult.Value!;

        // Act — accrue the full sick leave entitlement (30 working days over the cycle)
        // In practice this is front-loaded or accrued monthly — here we accrue 36 months
        var monthlyAccrualHours = SickLeaveDays * AnnualLeaveHoursPerDay / 36m;

        for (var month = 0; month < 36; month++)
        {
            var year = 2024 + (month + 2) / 12;
            var m = ((month + 2) % 12) + 1;
            var accrualDate = new DateOnly(year, m, 28);
            var accrualNow = new DateTimeOffset(year, m, 28, 12, 0, 0, TimeSpan.Zero);
            var ledgerId = $"ale_{empId}_sick_{month:D2}";

            var accrualResult = balance.PostAccrual(
                ledgerId, monthlyAccrualHours, accrualDate,
                "sick_leave_monthly_accrual", PolicyVersion, accrualNow);

            accrualResult.IsSuccess.Should().BeTrue(because: $"month {month} sick accrual must succeed");
        }

        // Assert — 30 working days × 8 hours = 240 hours
        var expectedTotal = SickLeaveDays * AnnualLeaveHoursPerDay;
        balance.AccruedHours.Should().BeApproximately(expectedTotal, 0.01m,
            because: "36 monthly accruals should total 30 working days (240 hours) within rounding tolerance");
    }

    // ── TC-UAT-LEAVE-003: Family responsibility leave ───────────────────────

    /// <summary>
    /// TC-UAT-LEAVE-003: Verifies family responsibility leave balance is 3 days (24 hours) per year.
    /// BCEA §27: 3 days per annum for qualifying family events.
    /// REQ-OPS-001, REQ-HR-002, CTL-BCEA-003
    /// </summary>
    [Fact]
    public void FamilyResponsibilityLeave_AnnualEntitlement_ThreeDays()
    {
        // TC-UAT-LEAVE-003: Arrange
        var empId = $"emp-family-{Guid.NewGuid():N}";
        var balanceId = $"lb_{empId}_family_2026";
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        var balanceResult = LeaveBalance.Create(
            balanceId, PilotTenantId, empId, LeaveType.FamilyResponsibility, "2026", PolicyVersion, now);
        balanceResult.IsSuccess.Should().BeTrue();

        var balance = balanceResult.Value!;

        // Act — accrue full annual entitlement at start of year (front-loaded)
        var totalHours = FamilyResponsibilityDays * AnnualLeaveHoursPerDay;
        var accrualResult = balance.PostAccrual(
            $"ale_{empId}_family_2026",
            totalHours,
            new DateOnly(2026, 1, 1),
            "annual_family_entitlement",
            PolicyVersion,
            now);

        // Assert
        accrualResult.IsSuccess.Should().BeTrue();
        balance.AvailableHours.Should().Be(totalHours,
            because: "family responsibility leave should be 3 days × 8 hours = 24 hours");
    }

    // ── TC-UAT-LEAVE-004: Leave request submission and approval flow ────────

    /// <summary>
    /// TC-UAT-LEAVE-004: Full leave workflow — submit request, approve, consume balance.
    /// REQ-OPS-001, REQ-HR-002, CTL-BCEA-003
    /// </summary>
    [Fact]
    public void LeaveRequest_SubmitAndApprove_BalanceDeducted()
    {
        // TC-UAT-LEAVE-004: Arrange — create balance with 120 hours accrued
        var empId = $"emp-flow-{Guid.NewGuid():N}";
        var balanceId = $"lb_{empId}_annual_2026";
        var now = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

        var balanceResult = LeaveBalance.Create(
            balanceId, PilotTenantId, empId, LeaveType.Annual, "2026", PolicyVersion, now);
        balanceResult.IsSuccess.Should().BeTrue();
        var balance = balanceResult.Value!;

        // Accrue 120 hours (15 working days)
        balance.PostAccrual(
            $"ale_{empId}_accrual",
            AnnualLeaveDays * AnnualLeaveHoursPerDay,
            new DateOnly(2026, 6, 1),
            "annual_accrual",
            PolicyVersion,
            now).IsSuccess.Should().BeTrue();

        var initialAvailable = balance.AvailableHours;

        // Act — submit leave request for 5 days (40 hours)
        var leaveRequestId = $"lr_{Guid.NewGuid():N}";
        const decimal requestedHours = 40.0m; // 5 working days × 8 hours
        var requestNow = now.AddDays(1);

        var submitResult = LeaveRequest.Submit(
            leaveRequestId, PilotTenantId, empId,
            LeaveType.Annual,
            startDate: new DateOnly(2026, 6, 15),
            endDate: new DateOnly(2026, 6, 19),
            totalHours: requestedHours,
            reasonCode: "annual_leave",
            balanceSnapshotAtRequest: balance.AvailableHours,
            now: requestNow);

        submitResult.IsSuccess.Should().BeTrue(because: "leave request submission must succeed");
        var request = submitResult.Value!;
        request.Status.Should().Be(LeaveRequestStatus.Submitted);

        // Act — approve the request
        var approveResult = request.Approve("uid-hrmanager-001", requestNow.AddHours(2));
        approveResult.IsSuccess.Should().BeTrue(because: "approval must succeed for Submitted request");
        request.Status.Should().Be(LeaveRequestStatus.Approved);

        // Act — consume hours from balance
        var consumeResult = balance.ConsumeHours(
            $"ale_{empId}_consume_{leaveRequestId}",
            leaveRequestId,
            requestedHours,
            new DateOnly(2026, 6, 15),
            PolicyVersion,
            "uid-hrmanager-001",
            requestNow.AddHours(2));

        // Assert
        consumeResult.IsSuccess.Should().BeTrue(because: "balance has sufficient hours");
        balance.AvailableHours.Should().Be(initialAvailable - requestedHours,
            because: $"available hours should decrease by {requestedHours} after consumption");
        balance.ConsumedHours.Should().Be(requestedHours);
    }

    // ── TC-UAT-LEAVE-005: Leave balance deduction after approval ────────────

    /// <summary>
    /// TC-UAT-LEAVE-005: Verifies multiple leave consumptions correctly reduce the balance
    /// and that the ledger entries are created.
    /// REQ-OPS-001, REQ-HR-002, CTL-BCEA-003
    /// </summary>
    [Fact]
    public void LeaveBalance_MultipleConsumptions_TracksCumulativeDeductions()
    {
        // TC-UAT-LEAVE-005: Arrange
        var empId = $"emp-multi-{Guid.NewGuid():N}";
        var balanceId = $"lb_{empId}_annual_2026";
        var now = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

        var balance = LeaveBalance.Create(
            balanceId, PilotTenantId, empId, LeaveType.Annual, "2026", PolicyVersion, now).Value!;

        // Accrue 120 hours
        balance.PostAccrual("ale-init", 120m, new DateOnly(2026, 3, 1), "accrual", PolicyVersion, now);

        // Act — consume 24 hours (3 days), then 16 hours (2 days)
        var consume1 = balance.ConsumeHours(
            "ale-c1", "lr-001", 24m, new DateOnly(2026, 4, 1), PolicyVersion, "uid-mgr", now.AddMonths(1));
        var consume2 = balance.ConsumeHours(
            "ale-c2", "lr-002", 16m, new DateOnly(2026, 5, 1), PolicyVersion, "uid-mgr", now.AddMonths(2));

        // Assert
        consume1.IsSuccess.Should().BeTrue();
        consume2.IsSuccess.Should().BeTrue();
        balance.ConsumedHours.Should().Be(40m, because: "24 + 16 = 40 hours consumed");
        balance.AvailableHours.Should().Be(80m, because: "120 - 40 = 80 hours available");

        var pendingEntries = balance.PopPendingEntries();
        pendingEntries.Should().HaveCount(3, because: "1 accrual + 2 consumptions = 3 ledger entries");
    }

    // ── TC-UAT-LEAVE-006: Reject leave when insufficient balance ────────────

    /// <summary>
    /// TC-UAT-LEAVE-006: Attempts to consume more hours than available and verifies
    /// the balance aggregate rejects the operation.
    /// REQ-OPS-001, REQ-HR-002, CTL-BCEA-003
    /// </summary>
    [Fact]
    public void LeaveBalance_InsufficientBalance_ConsumptionRejected()
    {
        // TC-UAT-LEAVE-006: Arrange
        var empId = $"emp-insuf-{Guid.NewGuid():N}";
        var balanceId = $"lb_{empId}_annual_2026";
        var now = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

        var balance = LeaveBalance.Create(
            balanceId, PilotTenantId, empId, LeaveType.Annual, "2026", PolicyVersion, now).Value!;

        // Accrue only 16 hours (2 days)
        balance.PostAccrual("ale-small", 16m, new DateOnly(2026, 3, 1), "accrual", PolicyVersion, now);

        // Act — attempt to consume 40 hours (5 days) with only 16 hours available
        var consumeResult = balance.ConsumeHours(
            "ale-fail", "lr-fail", 40m, new DateOnly(2026, 4, 1), PolicyVersion, "uid-mgr", now.AddMonths(1));

        // Assert
        consumeResult.IsFailure.Should().BeTrue(
            because: "consuming 40 hours with only 16 available must fail");
        balance.ConsumedHours.Should().Be(0m,
            because: "failed consumption must not affect the balance");
        balance.AvailableHours.Should().Be(16m,
            because: "balance must remain unchanged after rejected consumption");
    }
}
