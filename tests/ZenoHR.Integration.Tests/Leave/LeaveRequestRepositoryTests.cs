// TC-LEAVE-002: LeaveRequestRepository integration tests.
// REQ-HR-002: Leave request state machine persistence.
// CTL-BCEA-004: Pending leave query for manager approval queue.

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Integration.Tests.Infrastructure;
using ZenoHR.Module.Leave.Aggregates;

namespace ZenoHR.Integration.Tests.Leave;

/// <summary>
/// Integration tests for <see cref="LeaveRequestRepository"/> against the Firestore emulator.
/// TC-LEAVE-002-A: Submit and retrieve leave request.
/// TC-LEAVE-002-B: Approve transitions status to Approved.
/// TC-LEAVE-002-C: ListByEmployee returns newest-first order.
/// TC-LEAVE-002-D: ListPendingForEmployees returns only submitted/manager_review.
/// TC-LEAVE-002-E: Tenant isolation.
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class LeaveRequestRepositoryTests : IntegrationTestBase
{
    private readonly LeaveRequestRepository _repo;

    public LeaveRequestRepositoryTests(FirestoreEmulatorFixture fixture) : base(fixture)
    {
        _repo = new LeaveRequestRepository(fixture.Db, NullLogger<LeaveRequestRepository>.Instance);
    }

    // ── TC-LEAVE-002-A: Submit and retrieve ───────────────────────────────────

    [Fact]
    public async Task SaveAsync_ThenGetById_ReturnsLeaveRequest()
    {
        // Arrange
        var empId = $"emp_{Guid.CreateVersion7()}";
        var request = SubmitRequest(empId, TenantId, LeaveType.Annual,
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5), 40m);

        // Act
        var saveResult = await _repo.SaveAsync(request);
        var getResult = await _repo.GetByLeaveRequestIdAsync(TenantId, request.LeaveRequestId);

        // Assert
        saveResult.IsSuccess.Should().BeTrue();
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value!.EmployeeId.Should().Be(empId);
        getResult.Value.LeaveType.Should().Be(LeaveType.Annual);
        getResult.Value.Status.Should().Be(LeaveRequestStatus.Submitted);
        getResult.Value.TotalHours.Should().Be(40m);
    }

    // ── TC-LEAVE-002-B: Approve transitions status ────────────────────────────

    [Fact]
    public async Task Approve_PersistsApprovedStatus()
    {
        // Arrange
        var empId = $"emp_{Guid.CreateVersion7()}";
        var request = SubmitRequest(empId, TenantId, LeaveType.Sick,
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 2), 8m);
        await _repo.SaveAsync(request);

        // Act — approve
        request.Approve("approver-001", DateTimeOffset.UtcNow);
        await _repo.SaveAsync(request);

        // Assert
        var fetched = await _repo.GetByLeaveRequestIdAsync(TenantId, request.LeaveRequestId);
        fetched.Value!.Status.Should().Be(LeaveRequestStatus.Approved);
        fetched.Value.ApproverId.Should().Be("approver-001");
    }

    // ── TC-LEAVE-002-C: ListByEmployee ordering ───────────────────────────────

    [Fact]
    public async Task ListByEmployeeAsync_ReturnsRequestsForEmployee()
    {
        // Arrange
        var empId = $"emp_{Guid.CreateVersion7()}";
        var r1 = SubmitRequest(empId, TenantId, LeaveType.Annual,
            new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 14), 40m);
        var r2 = SubmitRequest(empId, TenantId, LeaveType.Sick,
            new DateOnly(2026, 2, 3), new DateOnly(2026, 2, 3), 8m);

        await _repo.SaveAsync(r1);
        await _repo.SaveAsync(r2);

        // Act
        var results = await _repo.ListByEmployeeAsync(TenantId, empId);

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.EmployeeId == empId);
    }

    // ── TC-LEAVE-002-D: Pending queue for manager approval ────────────────────

    [Fact]
    public async Task ListPendingForEmployeesAsync_ReturnsOnlyPendingStatuses()
    {
        // Arrange
        var empA = $"emp_{Guid.CreateVersion7()}";
        var empB = $"emp_{Guid.CreateVersion7()}";

        var pending = SubmitRequest(empA, TenantId, LeaveType.Annual,
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5), 40m);
        await _repo.SaveAsync(pending);

        var approved = SubmitRequest(empB, TenantId, LeaveType.Annual,
            new DateOnly(2026, 5, 8), new DateOnly(2026, 5, 9), 16m);
        approved.Approve("manager-001", DateTimeOffset.UtcNow);
        await _repo.SaveAsync(approved);

        // Act
        var results = await _repo.ListPendingForEmployeesAsync(TenantId, [empA, empB]);

        // Assert — only the submitted request, not the approved one
        results.Should().Contain(r => r.LeaveRequestId == pending.LeaveRequestId);
        results.Should().NotContain(r => r.LeaveRequestId == approved.LeaveRequestId);
    }

    // ── TC-LEAVE-002-E: Tenant isolation ─────────────────────────────────────

    [Fact]
    public async Task GetByLeaveRequestIdAsync_WrongTenant_ReturnsFailure()
    {
        // Arrange
        var empId = $"emp_{Guid.CreateVersion7()}";
        var request = SubmitRequest(empId, TenantId, LeaveType.Annual,
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), 40m);
        await _repo.SaveAsync(request);

        // Act
        var result = await _repo.GetByLeaveRequestIdAsync("other-tenant", request.LeaveRequestId);

        // Assert
        result.IsFailure.Should().BeTrue(because: "cross-tenant access must be blocked");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static LeaveRequest SubmitRequest(
        string empId, string tenantId, LeaveType leaveType,
        DateOnly start, DateOnly end, decimal hours)
    {
        var result = LeaveRequest.Submit(
            leaveRequestId: $"lr_{Guid.CreateVersion7()}",
            tenantId: tenantId,
            employeeId: empId,
            leaveType: leaveType,
            startDate: start,
            endDate: end,
            totalHours: hours,
            reasonCode: "test",
            balanceSnapshotAtRequest: null,
            now: DateTimeOffset.UtcNow);
        return result.Value!;
    }
}
