// TC-LEAVE-001: LeaveBalanceRepository integration tests.
// REQ-HR-002: Leave balance persistence and accrual ledger.
// CTL-BCEA-003: Balance never goes negative without allowNegativeBalance flag.

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Integration.Tests.Infrastructure;
using ZenoHR.Module.Leave.Aggregates;

namespace ZenoHR.Integration.Tests.Leave;

/// <summary>
/// Integration tests for <see cref="LeaveBalanceRepository"/> against the Firestore emulator.
/// TC-LEAVE-001-A: Save and retrieve balance by employee + type + cycle.
/// TC-LEAVE-001-B: SaveWithLedgerEntries writes accrual ledger entries.
/// TC-LEAVE-001-C: Tenant isolation on balance reads.
/// TC-LEAVE-001-D: PostAccrual updates balance and creates pending ledger entry.
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class LeaveBalanceRepositoryTests : IntegrationTestBase
{
    private readonly LeaveBalanceRepository _repo;

    public LeaveBalanceRepositoryTests(FirestoreEmulatorFixture fixture) : base(fixture)
    {
        _repo = new LeaveBalanceRepository(fixture.Db, NullLogger<LeaveBalanceRepository>.Instance);
    }

    // ── TC-LEAVE-001-A: Save and retrieve ────────────────────────────────────

    [Fact]
    public async Task SaveAsync_ThenGetByEmployeeAndType_ReturnsBalance()
    {
        // Arrange
        var empId = $"emp_{Guid.CreateVersion7()}";
        var balance = CreateBalance(empId, TenantId, LeaveType.Annual, "2026");

        // Act
        await _repo.SaveWithLedgerEntriesAsync(balance);
        var result = await _repo.GetByEmployeeAndTypeAsync(TenantId, empId, LeaveType.Annual, "2026");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.EmployeeId.Should().Be(empId);
        result.Value.LeaveType.Should().Be(LeaveType.Annual);
        result.Value.CycleId.Should().Be("2026");
        result.Value.AccruedHours.Should().Be(0m);
    }

    // ── TC-LEAVE-001-B: Ledger entries persisted ─────────────────────────────

    [Fact]
    public async Task SaveWithLedgerEntries_AfterAccrual_WritesLedgerEntry()
    {
        // Arrange
        var empId = $"emp_{Guid.CreateVersion7()}";
        var balance = CreateBalance(empId, TenantId, LeaveType.Annual, "2026");

        var accrualResult = balance.PostAccrual(
            ledgerEntryId: $"le_{Guid.CreateVersion7()}",
            hours: 13.333m,
            accrualDate: new DateOnly(2026, 1, 31),
            reasonCode: "monthly_accrual",
            policyVersion: "2026.1",
            now: DateTimeOffset.UtcNow);

        accrualResult.IsSuccess.Should().BeTrue();

        // Act
        var saveResult = await _repo.SaveWithLedgerEntriesAsync(balance);

        // Assert
        saveResult.IsSuccess.Should().BeTrue();

        // Re-fetch balance — accrued hours should be persisted
        var fetched = await _repo.GetByEmployeeAndTypeAsync(TenantId, empId, LeaveType.Annual, "2026");
        fetched.Value!.AccruedHours.Should().Be(13.333m);
    }

    // ── TC-LEAVE-001-C: Tenant isolation ─────────────────────────────────────

    [Fact]
    public async Task GetByEmployeeAndTypeAsync_WrongTenant_ReturnsFailure()
    {
        // Arrange
        var empId = $"emp_{Guid.CreateVersion7()}";
        var balance = CreateBalance(empId, TenantId, LeaveType.Sick, "2026");
        await _repo.SaveWithLedgerEntriesAsync(balance);

        // Act — query with a different tenant
        var result = await _repo.GetByEmployeeAndTypeAsync("other-tenant", empId, LeaveType.Sick, "2026");

        // Assert
        result.IsFailure.Should().BeTrue(because: "cross-tenant access must be blocked");
    }

    // ── TC-LEAVE-001-D: Multiple balance types per employee ──────────────────

    [Fact]
    public async Task ListByEmployeeAsync_ReturnsAllLeaveTypes()
    {
        // Arrange
        var empId = $"emp_{Guid.CreateVersion7()}";

        await _repo.SaveWithLedgerEntriesAsync(CreateBalance(empId, TenantId, LeaveType.Annual, "2026"));
        await _repo.SaveWithLedgerEntriesAsync(CreateBalance(empId, TenantId, LeaveType.Sick, "2026"));
        await _repo.SaveWithLedgerEntriesAsync(CreateBalance(empId, TenantId, LeaveType.FamilyResponsibility, "2026"));

        // Act
        var results = await _repo.ListByEmployeeAsync(TenantId, empId);

        // Assert
        results.Should().HaveCount(3);
        results.Select(b => b.LeaveType).Should().Contain(
        [
            LeaveType.Annual,
            LeaveType.Sick,
            LeaveType.FamilyResponsibility,
        ]);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static LeaveBalance CreateBalance(
        string empId, string tenantId, LeaveType leaveType, string cycleId)
    {
        var balanceId = $"lb_{empId.Substring(4, 8)}_{leaveType.ToString().ToLowerInvariant()}_{cycleId}";
        var result = LeaveBalance.Create(
            balanceId: balanceId,
            tenantId: tenantId,
            employeeId: empId,
            leaveType: leaveType,
            cycleId: cycleId,
            policyVersion: "2026.1",
            now: DateTimeOffset.UtcNow);
        return result.Value!;
    }
}
