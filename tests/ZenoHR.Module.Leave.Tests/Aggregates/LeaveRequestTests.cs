// TC-LEAVE-001: LeaveRequest aggregate — submit, approve, reject, cancel state machine.
// REQ-HR-002, CTL-BCEA-003, CTL-BCEA-004.
using FluentAssertions;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Leave.Aggregates;

namespace ZenoHR.Module.Leave.Tests.Aggregates;

public sealed class LeaveRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Start = new(2026, 3, 10);
    private static readonly DateOnly End = new(2026, 3, 12);

    // ── Submit factory ────────────────────────────────────────────────────────

    private static Result<LeaveRequest> ValidSubmit(
        string? id = "lr_001",
        string? tenantId = "tenant_001",
        string? empId = "emp_001",
        LeaveType type = LeaveType.Annual,
        DateOnly? start = null,
        DateOnly? end = null,
        decimal hours = 24m,
        string? reason = "ANNUAL_LEAVE")
        => LeaveRequest.Submit(id!, tenantId!, empId!, type,
            start ?? Start, end ?? End, hours, reason!, null, Now);

    [Fact]
    public void Submit_ValidInput_ReturnsSuccess()
    {
        // TC-LEAVE-001-001
        var result = ValidSubmit();

        result.IsSuccess.Should().BeTrue();
        result.Value!.LeaveRequestId.Should().Be("lr_001");
        result.Value.Status.Should().Be(LeaveRequestStatus.Submitted);
        result.Value.LeaveType.Should().Be(LeaveType.Annual);
        result.Value.TotalHours.Should().Be(24m);
        result.Value.ApproverId.Should().BeNull();
    }

    [Fact]
    public void Submit_RaisesSubmittedEvent()
    {
        // TC-LEAVE-001-002
        var result = ValidSubmit();
        var events = result.Value!.PopDomainEvents();

        events.Should().HaveCount(1);
        events[0].GetType().Name.Should().Be("LeaveRequestSubmittedEvent");
    }

    [Fact]
    public void Submit_EndDateBeforeStartDate_ReturnsFailure()
    {
        // TC-LEAVE-001-003
        var result = ValidSubmit(start: End, end: Start);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValueOutOfRange);
    }

    [Fact]
    public void Submit_ZeroHours_ReturnsFailure()
    {
        // TC-LEAVE-001-004
        var result = ValidSubmit(hours: 0m);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValueOutOfRange);
    }

    [Fact]
    public void Submit_NegativeHours_ReturnsFailure()
    {
        // TC-LEAVE-001-005
        var result = ValidSubmit(hours: -8m);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValueOutOfRange);
    }

    [Fact]
    public void Submit_UnknownLeaveType_ReturnsFailure()
    {
        // TC-LEAVE-001-006
        var result = ValidSubmit(type: LeaveType.Unknown);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.InvalidLeaveType);
    }

    [Fact]
    public void Submit_EmptyLeaveRequestId_ReturnsValidationFailure()
    {
        // TC-LEAVE-001-007
        var result = ValidSubmit(id: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    [Fact]
    public void Submit_EmptyTenantId_ReturnsValidationFailure()
    {
        // TC-LEAVE-001-008
        var result = ValidSubmit(tenantId: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    [Fact]
    public void Submit_EmptyEmployeeId_ReturnsValidationFailure()
    {
        // TC-LEAVE-001-009
        var result = ValidSubmit(empId: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    [Fact]
    public void Submit_EmptyReasonCode_ReturnsValidationFailure()
    {
        // TC-LEAVE-001-010
        var result = ValidSubmit(reason: "");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    [Fact]
    public void Submit_WithBalanceSnapshot_SetsSnapshot()
    {
        // TC-LEAVE-001-011 — informational balance snapshot preserved
        var result = LeaveRequest.Submit(
            "lr_001", "tenant_001", "emp_001",
            LeaveType.Annual, Start, End, 24m, "ANNUAL_LEAVE",
            balanceSnapshotAtRequest: 120m, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value!.BalanceSnapshotAtRequest.Should().Be(120m);
    }

    // ── Approve ───────────────────────────────────────────────────────────────

    [Fact]
    public void Approve_FromSubmitted_Succeeds()
    {
        // TC-LEAVE-001-020
        var req = ValidSubmit().Value!;

        var result = req.Approve("mgr_001", Now.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        req.Status.Should().Be(LeaveRequestStatus.Approved);
        req.ApproverId.Should().Be("mgr_001");
        req.ApprovedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Approve_RaisesApprovedEvent()
    {
        // TC-LEAVE-001-021
        var req = ValidSubmit().Value!;
        req.PopDomainEvents(); // clear submit event

        req.Approve("mgr_001", Now.AddHours(1));
        var events = req.PopDomainEvents();

        events.Should().HaveCount(1);
        events[0].GetType().Name.Should().Be("LeaveRequestApprovedEvent");
    }

    [Fact]
    public void Approve_FromManagerReview_Succeeds()
    {
        // TC-LEAVE-001-022
        var req = ValidSubmit().Value!;
        req.SendToManagerReview("mgr_001", Now.AddMinutes(10));

        var result = req.Approve("mgr_001", Now.AddHours(2));

        result.IsSuccess.Should().BeTrue();
        req.Status.Should().Be(LeaveRequestStatus.Approved);
    }

    [Fact]
    public void Approve_AlreadyApproved_ReturnsFailure()
    {
        // TC-LEAVE-001-023 — idempotency guard
        var req = ValidSubmit().Value!;
        req.Approve("mgr_001", Now.AddHours(1));

        var result = req.Approve("mgr_001", Now.AddHours(2));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.LeaveRequestAlreadyProcessed);
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    [Fact]
    public void Reject_FromSubmitted_Succeeds()
    {
        // TC-LEAVE-001-030
        var req = ValidSubmit().Value!;

        var result = req.Reject("mgr_001", "Operational coverage required", Now.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        req.Status.Should().Be(LeaveRequestStatus.Rejected);
        req.RejectionReason.Should().Be("Operational coverage required");
    }

    [Fact]
    public void Reject_RaisesRejectedEvent()
    {
        // TC-LEAVE-001-031
        var req = ValidSubmit().Value!;
        req.PopDomainEvents();

        req.Reject("mgr_001", "Operational coverage required", Now.AddHours(1));
        var events = req.PopDomainEvents();

        events.Should().HaveCount(1);
        events[0].GetType().Name.Should().Be("LeaveRequestRejectedEvent");
    }

    [Fact]
    public void Reject_EmptyRejectionReason_ReturnsFailure()
    {
        // TC-LEAVE-001-032
        var req = ValidSubmit().Value!;

        var result = req.Reject("mgr_001", "", Now.AddHours(1));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_FromSubmitted_Succeeds()
    {
        // TC-LEAVE-001-040
        var req = ValidSubmit().Value!;

        var result = req.Cancel("emp_001", Now.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        req.Status.Should().Be(LeaveRequestStatus.Cancelled);
    }

    [Fact]
    public void Cancel_AlreadyApproved_ReturnsFailure()
    {
        // TC-LEAVE-001-041
        var req = ValidSubmit().Value!;
        req.Approve("mgr_001", Now.AddHours(1));

        var result = req.Cancel("emp_001", Now.AddHours(2));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.LeaveRequestAlreadyProcessed);
    }

    [Fact]
    public void Cancel_AlreadyCancelled_ReturnsFailure()
    {
        // TC-LEAVE-001-042
        var req = ValidSubmit().Value!;
        req.Cancel("emp_001", Now.AddHours(1));

        var result = req.Cancel("emp_001", Now.AddHours(2));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.LeaveRequestAlreadyProcessed);
    }

    // ── SendToManagerReview ───────────────────────────────────────────────────

    [Fact]
    public void SendToManagerReview_FromSubmitted_Succeeds()
    {
        // TC-LEAVE-001-050
        var req = ValidSubmit().Value!;

        var result = req.SendToManagerReview("mgr_001", Now.AddMinutes(5));

        result.IsSuccess.Should().BeTrue();
        req.Status.Should().Be(LeaveRequestStatus.ManagerReview);
    }

    [Fact]
    public void SendToManagerReview_FromApproved_ReturnsFailure()
    {
        // TC-LEAVE-001-051
        var req = ValidSubmit().Value!;
        req.Approve("mgr_001", Now.AddHours(1));

        var result = req.SendToManagerReview("mgr_001", Now.AddHours(2));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ZenoHrErrorCode.LeaveRequestAlreadyProcessed);
    }

    // ── Reconstitution ────────────────────────────────────────────────────────

    [Fact]
    public void Reconstitute_SetsAllProperties()
    {
        // TC-LEAVE-001-060
        var req = LeaveRequest.Reconstitute(
            "lr_fs_001", "tenant_fs", "emp_fs_001",
            LeaveType.Sick, Start, End, 16m, "SICK_LEAVE", 80m,
            LeaveRequestStatus.Approved, "mgr_001", Now.AddHours(1),
            null, Now, Now.AddHours(1), "emp_fs_001");

        req.LeaveRequestId.Should().Be("lr_fs_001");
        req.Status.Should().Be(LeaveRequestStatus.Approved);
        req.ApproverId.Should().Be("mgr_001");
        req.LeaveType.Should().Be(LeaveType.Sick);
    }
}
