// TC-OPS-005: ClockEntryRepository integration tests against Firestore emulator.
// REQ-OPS-003: Employee self-service clock-in/clock-out persistence.
// REQ-SEC-005: Tenant isolation enforced on clock entry documents.
// Tests decimal precision on calculated_hours (stored as string) after Batch 1 fix.

using FluentAssertions;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Abstractions;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Integration.Tests.Infrastructure;
using ZenoHR.Module.TimeAttendance;

namespace ZenoHR.Integration.Tests.TimeAttendance;

/// <summary>
/// Integration tests for <see cref="ClockEntryRepository"/> against the Firestore emulator.
/// TC-OPS-005-A: ClockIn then GetByEntryId returns correct entry.
/// TC-OPS-005-B: GetOpenEntryAsync finds open entry for employee on date.
/// TC-OPS-005-C: ListByEmployeeAndDateRangeAsync returns entries in range.
/// TC-OPS-005-D: ClockOut persists calculated_hours with decimal precision (string storage).
/// TC-OPS-005-E: Tenant isolation — entry from different tenant is not accessible.
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class ClockEntryRepositoryTests : IntegrationTestBase
{
    // TC-OPS-005
    private readonly ClockEntryRepository _repo;

    public ClockEntryRepositoryTests(FirestoreEmulatorFixture fixture) : base(fixture)
    {
        _repo = new ClockEntryRepository(fixture.Db, NullLogger<ClockEntryRepository>.Instance);
    }

    // ── TC-OPS-005-A: ClockIn then GetByEntryId round-trip ────────────────────

    [Fact]
    public async Task SaveAsync_ThenGetByEntryId_ReturnsCorrectEntry()
    {
        // TC-OPS-005-A: Arrange
        var entryId = $"ce_{Guid.NewGuid():N}";
        var employeeId = $"emp_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        var clockInResult = ClockEntry.ClockIn(entryId, TenantId, employeeId, ClockEntrySource.EmployeeSelf, now);
        clockInResult.IsSuccess.Should().BeTrue();

        // Act
        var saveResult = await _repo.SaveAsync(clockInResult.Value!);
        var getResult = await _repo.GetByEntryIdAsync(TenantId, entryId);

        // Assert
        saveResult.IsSuccess.Should().BeTrue();
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value!.EntryId.Should().Be(entryId);
        getResult.Value.TenantId.Should().Be(TenantId);
        getResult.Value.EmployeeId.Should().Be(employeeId);
        getResult.Value.Status.Should().Be(ClockEntryStatus.Open);
        getResult.Value.Source.Should().Be(ClockEntrySource.EmployeeSelf);
    }

    // ── TC-OPS-005-B: GetOpenEntryAsync finds open entry ──────────────────────

    [Fact]
    public async Task GetOpenEntryAsync_WithOpenEntry_ReturnsEntry()
    {
        // TC-OPS-005-B: Arrange
        var entryId = $"ce_{Guid.NewGuid():N}";
        var employeeId = $"emp_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var date = DateOnly.FromDateTime(now.UtcDateTime);

        var entry = ClockEntry.ClockIn(entryId, TenantId, employeeId, ClockEntrySource.EmployeeSelf, now).Value!;
        await _repo.SaveAsync(entry);

        // Act
        var result = await _repo.GetOpenEntryAsync(TenantId, employeeId, date);

        // Assert
        result.Should().NotBeNull();
        result!.EntryId.Should().Be(entryId);
        result.Status.Should().Be(ClockEntryStatus.Open);
    }

    // ── TC-OPS-005-C: ListByEmployeeAndDateRangeAsync ─────────────────────────

    [Fact]
    public async Task ListByEmployeeAndDateRangeAsync_ReturnsEntriesInRange()
    {
        // TC-OPS-005-C: Arrange — create entry for today
        var entryId = $"ce_{Guid.NewGuid():N}";
        var employeeId = $"emp_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var entry = ClockEntry.ClockIn(entryId, TenantId, employeeId, ClockEntrySource.EmployeeSelf, now).Value!;
        await _repo.SaveAsync(entry);

        // Act — query range that includes today
        var from = today.AddDays(-1);
        var to = today.AddDays(1);
        var results = await _repo.ListByEmployeeAndDateRangeAsync(TenantId, employeeId, from, to);

        // Assert
        results.Should().ContainSingle(e => e.EntryId == entryId);
    }

    // ── TC-OPS-005-D: Decimal precision on calculated_hours ───────────────────

    [Fact]
    public async Task ClockOut_PersistsCalculatedHoursWithDecimalPrecision()
    {
        // TC-OPS-005-D: Arrange — clock in, then clock out exactly 8.5 hours later
        var entryId = $"ce_{Guid.NewGuid():N}";
        var employeeId = $"emp_{Guid.NewGuid():N}";
        var clockIn = new DateTimeOffset(2026, 3, 13, 8, 0, 0, TimeSpan.Zero);
        var clockOut = clockIn.AddHours(8.5);
        var now = clockOut;

        var entry = ClockEntry.ClockIn(entryId, TenantId, employeeId, ClockEntrySource.EmployeeSelf, clockIn).Value!;
        entry.ClockOut(clockOut, now);
        await _repo.SaveAsync(entry);

        // Act
        var getResult = await _repo.GetByEntryIdAsync(TenantId, entryId);

        // Assert — calculated_hours stored as string must preserve decimal precision
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value!.CalculatedHours.Should().Be(8.5m,
            because: "calculated_hours stored as string must preserve decimal precision after Batch 1 fix");
        getResult.Value.Status.Should().Be(ClockEntryStatus.Completed);
    }

    // ── TC-OPS-005-E: Tenant isolation ────────────────────────────────────────

    [Fact]
    public async Task GetByEntryIdAsync_WrongTenant_ReturnsFailure()
    {
        // TC-OPS-005-E: Arrange
        var entryId = $"ce_{Guid.NewGuid():N}";
        var employeeId = $"emp_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        var entry = ClockEntry.ClockIn(entryId, TenantId, employeeId, ClockEntrySource.EmployeeSelf, now).Value!;
        await _repo.SaveAsync(entry);

        // Act — attempt to read with a different tenant
        var otherTenant = $"test-tenant-{Guid.NewGuid():N}";
        var result = await _repo.GetByEntryIdAsync(otherTenant, entryId);

        // Assert
        result.IsFailure.Should().BeTrue(
            because: "REQ-SEC-005: entry owned by a different tenant must not be accessible");
    }
}
