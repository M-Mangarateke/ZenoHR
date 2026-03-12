// CTL-POPIA-009: Tests for POPIA §23 Subject Access Request workflow.

using FluentAssertions;
using ZenoHR.Module.Compliance.Models;
using ZenoHR.Module.Compliance.Services;

namespace ZenoHR.Module.Compliance.Tests.Popia;

public sealed class SubjectAccessRequestServiceTests
{
    private readonly SubjectAccessRequestService _service = new();

    private static SubjectAccessRequest CreateTestRequest(
        SarStatus status = SarStatus.Submitted,
        DateTimeOffset? requestedAt = null)
    {
        var requested = requestedAt ?? DateTimeOffset.UtcNow;
        return new SubjectAccessRequest
        {
            RequestId = "SAR-2026-0001",
            TenantId = "tenant-1",
            EmployeeId = "emp-001",
            RequestedAt = requested,
            RequestedBy = "emp-001",
            Status = status,
            DeadlineDate = DateOnly.FromDateTime(requested.UtcDateTime).AddDays(30)
        };
    }

    // ── SubmitRequest ─────────────────────────────────────────────────────

    [Fact]
    public void SubmitRequest_ValidInput_ReturnsRequestWith30DayDeadline()
    {
        var requestedAt = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

        var result = _service.SubmitRequest("tenant-1", "emp-001", "emp-001", requestedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(SarStatus.Submitted);
        result.Value.TenantId.Should().Be("tenant-1");
        result.Value.EmployeeId.Should().Be("emp-001");
        result.Value.RequestedBy.Should().Be("emp-001");
        result.Value.RequestId.Should().StartWith("SAR-");
        result.Value.DeadlineDate.Should().Be(new DateOnly(2026, 3, 31));
    }

    [Fact]
    public void SubmitRequest_EmptyTenantId_ReturnsFailure()
    {
        var result = _service.SubmitRequest("", "emp-001", "emp-001", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SubmitRequest_EmptyEmployeeId_ReturnsFailure()
    {
        var result = _service.SubmitRequest("tenant-1", "", "emp-001", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SubmitRequest_EmptyRequestedBy_ReturnsFailure()
    {
        var result = _service.SubmitRequest("tenant-1", "emp-001", "", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SubmitRequest_WhitespaceTenantId_ReturnsFailure()
    {
        var result = _service.SubmitRequest("   ", "emp-001", "emp-001", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    // ── ReviewRequest ─────────────────────────────────────────────────────

    [Fact]
    public void ReviewRequest_FromSubmitted_Succeeds()
    {
        var request = CreateTestRequest(SarStatus.Submitted);
        var now = DateTimeOffset.UtcNow;

        var result = _service.ReviewRequest(request, "hr-manager-001", now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(SarStatus.UnderReview);
        result.Value.ReviewedBy.Should().Be("hr-manager-001");
        result.Value.ReviewedAt.Should().Be(now);
    }

    [Fact]
    public void ReviewRequest_FromDataGathering_ReturnsFailure()
    {
        var request = CreateTestRequest(SarStatus.DataGathering);

        var result = _service.ReviewRequest(request, "hr-manager-001", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ReviewRequest_EmptyReviewedBy_ReturnsFailure()
    {
        var request = CreateTestRequest(SarStatus.Submitted);

        var result = _service.ReviewRequest(request, "", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    // ── CompleteRequest ───────────────────────────────────────────────────

    [Fact]
    public void CompleteRequest_FromDataGathering_Succeeds()
    {
        var request = CreateTestRequest(SarStatus.DataGathering);
        var now = DateTimeOffset.UtcNow;

        var result = _service.CompleteRequest(request, "hr-manager-001", now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(SarStatus.Completed);
        result.Value.CompletedAt.Should().Be(now);
        result.Value.DataPackageGeneratedAt.Should().Be(now);
    }

    [Fact]
    public void CompleteRequest_FromUnderReview_Succeeds()
    {
        var request = CreateTestRequest(SarStatus.UnderReview);
        var now = DateTimeOffset.UtcNow;

        var result = _service.CompleteRequest(request, "hr-manager-001", now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(SarStatus.Completed);
    }

    [Fact]
    public void CompleteRequest_FromCompleted_ReturnsFailure()
    {
        var request = CreateTestRequest(SarStatus.Completed);

        var result = _service.CompleteRequest(request, "hr-manager-001", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    // ── RejectRequest ─────────────────────────────────────────────────────

    [Fact]
    public void RejectRequest_WithReason_Succeeds()
    {
        var request = CreateTestRequest(SarStatus.Submitted);
        var now = DateTimeOffset.UtcNow;

        var result = _service.RejectRequest(request, "hr-manager-001", "Duplicate request", now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(SarStatus.Rejected);
        result.Value.RejectionReason.Should().Be("Duplicate request");
        result.Value.ReviewedBy.Should().Be("hr-manager-001");
        result.Value.ReviewedAt.Should().Be(now);
    }

    [Fact]
    public void RejectRequest_WithoutReason_ReturnsFailure()
    {
        var request = CreateTestRequest(SarStatus.Submitted);

        var result = _service.RejectRequest(request, "hr-manager-001", "", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RejectRequest_FromCompleted_ReturnsFailure()
    {
        var request = CreateTestRequest(SarStatus.Completed);

        var result = _service.RejectRequest(request, "hr-manager-001", "Reason", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RejectRequest_FromRejected_ReturnsFailure()
    {
        var request = CreateTestRequest(SarStatus.Rejected);

        var result = _service.RejectRequest(request, "hr-manager-001", "Reason", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    // ── IsOverdue / DaysRemaining ─────────────────────────────────────────

    [Fact]
    public void IsOverdue_After31Days_ReturnsTrue()
    {
        var requestedAt = DateTimeOffset.UtcNow.AddDays(-31);
        var request = CreateTestRequest(SarStatus.Submitted, requestedAt);

        request.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public void IsOverdue_WithinDeadline_ReturnsFalse()
    {
        var requestedAt = DateTimeOffset.UtcNow.AddDays(-5);
        var request = CreateTestRequest(SarStatus.Submitted, requestedAt);

        request.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_CompletedButPastDeadline_ReturnsFalse()
    {
        var requestedAt = DateTimeOffset.UtcNow.AddDays(-31);
        var request = CreateTestRequest(SarStatus.Completed, requestedAt);

        request.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_RejectedButPastDeadline_ReturnsFalse()
    {
        var requestedAt = DateTimeOffset.UtcNow.AddDays(-31);
        var request = CreateTestRequest(SarStatus.Rejected, requestedAt);

        request.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void DaysRemaining_FreshRequest_ReturnsPositive()
    {
        var requestedAt = DateTimeOffset.UtcNow;
        var request = CreateTestRequest(SarStatus.Submitted, requestedAt);

        request.DaysRemaining.Should().BeGreaterOrEqualTo(29);
    }

    [Fact]
    public void DaysRemaining_PastDeadline_ReturnsNegative()
    {
        var requestedAt = DateTimeOffset.UtcNow.AddDays(-35);
        var request = CreateTestRequest(SarStatus.Submitted, requestedAt);

        request.DaysRemaining.Should().BeNegative();
    }

    // ── GetOverdueRequests ────────────────────────────────────────────────

    [Fact]
    public void GetOverdueRequests_FiltersCorrectly()
    {
        var overdueRequest = CreateTestRequest(SarStatus.UnderReview, DateTimeOffset.UtcNow.AddDays(-31));
        var freshRequest = CreateTestRequest(SarStatus.Submitted, DateTimeOffset.UtcNow.AddDays(-1));
        var completedRequest = CreateTestRequest(SarStatus.Completed, DateTimeOffset.UtcNow.AddDays(-31));

        var result = _service.GetOverdueRequests([overdueRequest, freshRequest, completedRequest]);

        result.Should().HaveCount(1);
        result[0].Should().Be(overdueRequest);
    }

    [Fact]
    public void GetOverdueRequests_EmptyList_ReturnsEmpty()
    {
        var result = _service.GetOverdueRequests([]);

        result.Should().BeEmpty();
    }

    // ── Backward Status Transitions ───────────────────────────────────────

    [Fact]
    public void BackwardTransition_UnderReviewToSubmitted_ReturnsFailure()
    {
        // ReviewRequest targets UnderReview — can't go back. Test via CompleteRequest going backwards.
        var request = CreateTestRequest(SarStatus.Completed);

        var result = _service.CompleteRequest(request, "hr-manager-001", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ReviewRequest_FromCompleted_ReturnsFailure()
    {
        var request = CreateTestRequest(SarStatus.Completed);

        var result = _service.ReviewRequest(request, "hr-manager-001", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }
}
