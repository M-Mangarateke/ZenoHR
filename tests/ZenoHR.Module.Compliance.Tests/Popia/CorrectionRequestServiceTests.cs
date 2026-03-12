// CTL-POPIA-010: Tests for POPIA §24 correction of personal information workflow.

using FluentAssertions;
using ZenoHR.Module.Compliance.Models;
using ZenoHR.Module.Compliance.Services;

namespace ZenoHR.Module.Compliance.Tests.Popia;

public sealed class CorrectionRequestServiceTests
{
    private readonly CorrectionRequestService _service = new();
    private static readonly DateTimeOffset TestTimestamp = new(2026, 3, 12, 10, 0, 0, TimeSpan.Zero);

    private static CorrectionRequest CreateTestRequest(CorrectionStatus status = CorrectionStatus.Submitted)
    {
        return new CorrectionRequest
        {
            RequestId = "COR-2026-0001",
            TenantId = "tenant-1",
            EmployeeId = "emp-001",
            RequestedAt = TestTimestamp,
            RequestedBy = "user-001",
            FieldName = "Surname",
            CurrentValue = "Smith",
            ProposedValue = "Smyth",
            Reason = "Name was misspelled during onboarding.",
            Status = status
        };
    }

    // ── SubmitCorrection ──────────────────────────────────────────────────

    [Fact]
    public void SubmitCorrection_ValidInput_ReturnsSuccess()
    {
        var result = _service.SubmitCorrection(
            "tenant-1", "emp-001", "Surname", "Smith", "Smyth",
            "Name was misspelled.", "user-001", TestTimestamp);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(CorrectionStatus.Submitted);
        result.Value.RequestId.Should().StartWith("COR-");
        result.Value.TenantId.Should().Be("tenant-1");
        result.Value.EmployeeId.Should().Be("emp-001");
        result.Value.FieldName.Should().Be("Surname");
        result.Value.CurrentValue.Should().Be("Smith");
        result.Value.ProposedValue.Should().Be("Smyth");
    }

    [Fact]
    public void SubmitCorrection_EmptyTenantId_ReturnsFailure()
    {
        var result = _service.SubmitCorrection(
            "", "emp-001", "Surname", "Smith", "Smyth",
            "Reason", "user-001", TestTimestamp);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SubmitCorrection_EmptyEmployeeId_ReturnsFailure()
    {
        var result = _service.SubmitCorrection(
            "tenant-1", "", "Surname", "Smith", "Smyth",
            "Reason", "user-001", TestTimestamp);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SubmitCorrection_EmptyFieldName_ReturnsFailure()
    {
        var result = _service.SubmitCorrection(
            "tenant-1", "emp-001", "", "Smith", "Smyth",
            "Reason", "user-001", TestTimestamp);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SubmitCorrection_EmptyReason_ReturnsFailure()
    {
        var result = _service.SubmitCorrection(
            "tenant-1", "emp-001", "Surname", "Smith", "Smyth",
            "", "user-001", TestTimestamp);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SubmitCorrection_EmptyRequestedBy_ReturnsFailure()
    {
        var result = _service.SubmitCorrection(
            "tenant-1", "emp-001", "Surname", "Smith", "Smyth",
            "Reason", "", TestTimestamp);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SubmitCorrection_SameCurrentAndProposedValue_ReturnsFailure()
    {
        var result = _service.SubmitCorrection(
            "tenant-1", "emp-001", "Surname", "Smith", "Smith",
            "Reason", "user-001", TestTimestamp);

        result.IsFailure.Should().BeTrue();
    }

    // ── ApproveCorrection ─────────────────────────────────────────────────

    [Fact]
    public void ApproveCorrection_FromUnderReview_ReturnsSuccess()
    {
        var request = CreateTestRequest(CorrectionStatus.UnderReview);
        var now = DateTimeOffset.UtcNow;

        var result = _service.ApproveCorrection(request, "reviewer-001", now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(CorrectionStatus.Approved);
        result.Value.ReviewedBy.Should().Be("reviewer-001");
        result.Value.ReviewedAt.Should().Be(now);
    }

    [Fact]
    public void ApproveCorrection_EmptyReviewedBy_ReturnsFailure()
    {
        var request = CreateTestRequest(CorrectionStatus.UnderReview);

        var result = _service.ApproveCorrection(request, "", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ApproveCorrection_FromSubmitted_ReturnsFailure()
    {
        // Approved requires UnderReview (cannot skip from Submitted to Approved)
        var request = CreateTestRequest(CorrectionStatus.Submitted);

        var result = _service.ApproveCorrection(request, "reviewer-001", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ApproveCorrection_FromRejected_ReturnsFailure()
    {
        var request = CreateTestRequest(CorrectionStatus.Rejected);

        var result = _service.ApproveCorrection(request, "reviewer-001", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    // ── ApplyCorrection ───────────────────────────────────────────────────

    [Fact]
    public void ApplyCorrection_FromApproved_ReturnsSuccess()
    {
        var request = CreateTestRequest(CorrectionStatus.Approved);
        var now = DateTimeOffset.UtcNow;

        var result = _service.ApplyCorrection(request, now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(CorrectionStatus.Applied);
        result.Value.AppliedAt.Should().Be(now);
    }

    [Fact]
    public void ApplyCorrection_FromSubmitted_ReturnsFailure()
    {
        var request = CreateTestRequest(CorrectionStatus.Submitted);

        var result = _service.ApplyCorrection(request, DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ApplyCorrection_FromUnderReview_ReturnsFailure()
    {
        var request = CreateTestRequest(CorrectionStatus.UnderReview);

        var result = _service.ApplyCorrection(request, DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    // ── RejectCorrection ──────────────────────────────────────────────────

    [Fact]
    public void RejectCorrection_FromUnderReview_WithReason_ReturnsSuccess()
    {
        var request = CreateTestRequest(CorrectionStatus.UnderReview);
        var now = DateTimeOffset.UtcNow;

        var result = _service.RejectCorrection(request, "reviewer-001", "Original value is correct per ID document.", now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(CorrectionStatus.Rejected);
        result.Value.ReviewedBy.Should().Be("reviewer-001");
        result.Value.ReviewedAt.Should().Be(now);
        result.Value.RejectionReason.Should().Be("Original value is correct per ID document.");
    }

    [Fact]
    public void RejectCorrection_FromSubmitted_WithReason_ReturnsSuccess()
    {
        var request = CreateTestRequest(CorrectionStatus.Submitted);
        var now = DateTimeOffset.UtcNow;

        var result = _service.RejectCorrection(request, "reviewer-001", "Duplicate request.", now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(CorrectionStatus.Rejected);
    }

    [Fact]
    public void RejectCorrection_WithoutReason_ReturnsFailure()
    {
        var request = CreateTestRequest(CorrectionStatus.UnderReview);

        var result = _service.RejectCorrection(request, "reviewer-001", "", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RejectCorrection_WithoutReviewedBy_ReturnsFailure()
    {
        var request = CreateTestRequest(CorrectionStatus.UnderReview);

        var result = _service.RejectCorrection(request, "", "Reason.", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RejectCorrection_FromApproved_ReturnsFailure()
    {
        var request = CreateTestRequest(CorrectionStatus.Approved);

        var result = _service.RejectCorrection(request, "reviewer-001", "Too late.", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RejectCorrection_FromApplied_ReturnsFailure()
    {
        var request = CreateTestRequest(CorrectionStatus.Applied);

        var result = _service.RejectCorrection(request, "reviewer-001", "Too late.", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    // ── Backward transition (general) ─────────────────────────────────────

    [Fact]
    public void ApproveCorrection_FromApplied_BackwardTransition_ReturnsFailure()
    {
        var request = CreateTestRequest(CorrectionStatus.Applied);

        var result = _service.ApproveCorrection(request, "reviewer-001", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ApplyCorrection_FromRejected_ReturnsFailure()
    {
        var request = CreateTestRequest(CorrectionStatus.Rejected);

        var result = _service.ApplyCorrection(request, DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    // ── Immutability — original record unchanged ──────────────────────────

    [Fact]
    public void ApproveCorrection_DoesNotMutateOriginalRecord()
    {
        var original = CreateTestRequest(CorrectionStatus.UnderReview);

        _ = _service.ApproveCorrection(original, "reviewer-001", DateTimeOffset.UtcNow);

        original.Status.Should().Be(CorrectionStatus.UnderReview);
        original.ReviewedBy.Should().BeNull();
    }
}
