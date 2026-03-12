// REQ-SEC-008, VUL-006: Tests for break-glass emergency access procedure.

using FluentAssertions;
using ZenoHR.Module.Compliance.Models;
using ZenoHR.Module.Compliance.Services;

namespace ZenoHR.Module.Compliance.Tests.Security;

public sealed class BreakGlassServiceTests
{
    private readonly BreakGlassService _service = new();
    private static readonly DateTimeOffset Now = new(2026, 3, 12, 10, 0, 0, TimeSpan.Zero);

    private static BreakGlassRequest CreateTestRequest(
        BreakGlassStatus status = BreakGlassStatus.Requested,
        DateTimeOffset? approvedAt = null,
        DateTimeOffset? expiresAt = null)
    {
        return new BreakGlassRequest
        {
            RequestId = "BG-2026-0001",
            TenantId = "tenant-1",
            RequestedBy = "user-director-001",
            RequestedAt = Now,
            Reason = "Production payroll system outage — employees unpaid.",
            Urgency = BreakGlassUrgency.PayrollCrisis,
            Status = status,
            ApprovedBy = status >= BreakGlassStatus.Approved ? "saas-admin-001" : null,
            ApprovedAt = approvedAt ?? (status >= BreakGlassStatus.Approved ? Now : null),
            ExpiresAt = expiresAt ?? (status >= BreakGlassStatus.Approved ? Now.AddHours(BreakGlassRequest.DefaultExpiryHours) : null),
        };
    }

    // ── RequestAccess ─────────────────────────────────────────────────────

    [Fact]
    public void RequestAccess_ValidInput_ReturnsRequest()
    {
        var result = _service.RequestAccess(
            "tenant-1", "user-001", "System outage", BreakGlassUrgency.SystemOutage, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BreakGlassStatus.Requested);
        result.Value.RequestId.Should().StartWith("BG-");
        result.Value.TenantId.Should().Be("tenant-1");
        result.Value.RequestedBy.Should().Be("user-001");
        result.Value.Reason.Should().Be("System outage");
        result.Value.Urgency.Should().Be(BreakGlassUrgency.SystemOutage);
    }

    [Fact]
    public void RequestAccess_EmptyReason_ReturnsFailure()
    {
        var result = _service.RequestAccess(
            "tenant-1", "user-001", "", BreakGlassUrgency.SystemOutage, Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RequestAccess_EmptyTenantId_ReturnsFailure()
    {
        var result = _service.RequestAccess(
            "", "user-001", "Outage", BreakGlassUrgency.SystemOutage, Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RequestAccess_EmptyRequestedBy_ReturnsFailure()
    {
        var result = _service.RequestAccess(
            "tenant-1", "", "Outage", BreakGlassUrgency.SystemOutage, Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RequestAccess_UnknownUrgency_ReturnsFailure()
    {
        var result = _service.RequestAccess(
            "tenant-1", "user-001", "Outage", BreakGlassUrgency.Unknown, Now);

        result.IsFailure.Should().BeTrue();
    }

    // ── ApproveAccess ─────────────────────────────────────────────────────

    [Fact]
    public void ApproveAccess_FromRequested_SetsApprovalAndExpiry()
    {
        var request = CreateTestRequest(BreakGlassStatus.Requested);

        var result = _service.ApproveAccess(request, "saas-admin-001", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BreakGlassStatus.Approved);
        result.Value.ApprovedBy.Should().Be("saas-admin-001");
        result.Value.ApprovedAt.Should().Be(Now);
        result.Value.ExpiresAt.Should().Be(Now.AddHours(4));
    }

    [Fact]
    public void ApproveAccess_DefaultExpiry_Is4Hours()
    {
        var request = CreateTestRequest(BreakGlassStatus.Requested);

        var result = _service.ApproveAccess(request, "saas-admin-001", Now);

        result.IsSuccess.Should().BeTrue();
        var expectedExpiry = Now.AddHours(BreakGlassRequest.DefaultExpiryHours);
        result.Value.ExpiresAt.Should().Be(expectedExpiry);
    }

    [Fact]
    public void ApproveAccess_CustomExpiry_SetsCorrectExpiry()
    {
        var request = CreateTestRequest(BreakGlassStatus.Requested);

        var result = _service.ApproveAccess(request, "saas-admin-001", Now, expiryHours: 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.ExpiresAt.Should().Be(Now.AddHours(2));
    }

    [Fact]
    public void ApproveAccess_FromApproved_ReturnsFailure()
    {
        var request = CreateTestRequest(BreakGlassStatus.Approved);

        var result = _service.ApproveAccess(request, "saas-admin-002", Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ApproveAccess_EmptyApprovedBy_ReturnsFailure()
    {
        var request = CreateTestRequest(BreakGlassStatus.Requested);

        var result = _service.ApproveAccess(request, "", Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ApproveAccess_ZeroExpiryHours_ReturnsFailure()
    {
        var request = CreateTestRequest(BreakGlassStatus.Requested);

        var result = _service.ApproveAccess(request, "saas-admin-001", Now, expiryHours: 0);

        result.IsFailure.Should().BeTrue();
    }

    // ── RevokeAccess ──────────────────────────────────────────────────────

    [Fact]
    public void RevokeAccess_FromApproved_Succeeds()
    {
        var request = CreateTestRequest(BreakGlassStatus.Approved);

        var result = _service.RevokeAccess(request, "saas-admin-001", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BreakGlassStatus.Revoked);
        result.Value.RevokedBy.Should().Be("saas-admin-001");
        result.Value.RevokedAt.Should().Be(Now);
    }

    [Fact]
    public void RevokeAccess_FromRequested_ReturnsFailure()
    {
        var request = CreateTestRequest(BreakGlassStatus.Requested);

        var result = _service.RevokeAccess(request, "saas-admin-001", Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RevokeAccess_EmptyRevokedBy_ReturnsFailure()
    {
        var request = CreateTestRequest(BreakGlassStatus.Approved);

        var result = _service.RevokeAccess(request, "", Now);

        result.IsFailure.Should().BeTrue();
    }

    // ── CompletePostReview ─────────────────────────────────────────────────

    [Fact]
    public void CompletePostReview_FromExpired_Succeeds()
    {
        var request = CreateTestRequest(BreakGlassStatus.Expired);

        var result = _service.CompletePostReview(request, "auditor-001", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BreakGlassStatus.Closed);
        result.Value.PostReviewCompletedBy.Should().Be("auditor-001");
        result.Value.PostReviewCompletedAt.Should().Be(Now);
    }

    [Fact]
    public void CompletePostReview_FromRevoked_Succeeds()
    {
        var request = CreateTestRequest(BreakGlassStatus.Revoked);

        var result = _service.CompletePostReview(request, "auditor-001", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BreakGlassStatus.Closed);
    }

    [Fact]
    public void CompletePostReview_FromPostReviewPending_Succeeds()
    {
        var request = CreateTestRequest(BreakGlassStatus.PostReviewPending);

        var result = _service.CompletePostReview(request, "auditor-001", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BreakGlassStatus.Closed);
    }

    [Fact]
    public void CompletePostReview_FromApproved_ReturnsFailure()
    {
        var request = CreateTestRequest(BreakGlassStatus.Approved);

        var result = _service.CompletePostReview(request, "auditor-001", Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CompletePostReview_EmptyReviewedBy_ReturnsFailure()
    {
        var request = CreateTestRequest(BreakGlassStatus.Expired);

        var result = _service.CompletePostReview(request, "", Now);

        result.IsFailure.Should().BeTrue();
    }

    // ── IsExpired / IsActive ──────────────────────────────────────────────

    [Fact]
    public void IsExpired_AfterExpiryWindow_ReturnsTrue()
    {
        var pastExpiry = Now.AddHours(-5);
        var request = CreateTestRequest(
            BreakGlassStatus.Approved,
            approvedAt: pastExpiry,
            expiresAt: pastExpiry.AddHours(4)); // expired 1 hour ago

        request.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_BeforeExpiryWindow_ReturnsFalse()
    {
        var request = CreateTestRequest(
            BreakGlassStatus.Approved,
            approvedAt: Now,
            expiresAt: Now.AddHours(4));

        request.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsActive_ApprovedAndNotExpired_ReturnsTrue()
    {
        var request = CreateTestRequest(
            BreakGlassStatus.Approved,
            approvedAt: Now,
            expiresAt: Now.AddHours(4));

        request.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_ApprovedButExpired_ReturnsFalse()
    {
        var pastExpiry = Now.AddHours(-5);
        var request = CreateTestRequest(
            BreakGlassStatus.Approved,
            approvedAt: pastExpiry,
            expiresAt: pastExpiry.AddHours(4));

        request.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_RequestedStatus_ReturnsFalse()
    {
        var request = CreateTestRequest(BreakGlassStatus.Requested);

        request.IsActive.Should().BeFalse();
    }

    // ── GetActiveRequests ─────────────────────────────────────────────────

    [Fact]
    public void GetActiveRequests_FiltersCorrectly()
    {
        var activeRequest = CreateTestRequest(
            BreakGlassStatus.Approved,
            approvedAt: Now,
            expiresAt: Now.AddHours(4));

        var pastExpiry = Now.AddHours(-5);
        var expiredRequest = CreateTestRequest(
            BreakGlassStatus.Approved,
            approvedAt: pastExpiry,
            expiresAt: pastExpiry.AddHours(4)) with { RequestId = "BG-2026-0002" };

        var requestedOnly = CreateTestRequest(BreakGlassStatus.Requested) with { RequestId = "BG-2026-0003" };

        var revokedRequest = CreateTestRequest(BreakGlassStatus.Revoked) with { RequestId = "BG-2026-0004" };

        var requests = new List<BreakGlassRequest> { activeRequest, expiredRequest, requestedOnly, revokedRequest };

        var result = _service.GetActiveRequests(requests);

        result.Should().HaveCount(1);
        result[0].RequestId.Should().Be("BG-2026-0001");
    }

    [Fact]
    public void GetActiveRequests_EmptyList_ReturnsEmpty()
    {
        var result = _service.GetActiveRequests([]);

        result.Should().BeEmpty();
    }
}
