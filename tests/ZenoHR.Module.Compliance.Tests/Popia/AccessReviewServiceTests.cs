// CTL-POPIA-007: Tests for monthly access review service.

using FluentAssertions;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Models;
using ZenoHR.Module.Compliance.Services;

namespace ZenoHR.Module.Compliance.Tests.Popia;

public sealed class AccessReviewServiceTests
{
    private readonly AccessReviewService _service = new();

    private static RoleAssignmentEntry CreateAssignment(
        string employeeId = "emp-001",
        string roleName = "Employee",
        string departmentId = "dept-001",
        DateTimeOffset? assignedAt = null,
        DateTimeOffset? lastLoginAt = null,
        bool isTerminated = false) => new()
    {
        EmployeeId = employeeId,
        RoleName = roleName,
        DepartmentId = departmentId,
        AssignedAt = assignedAt ?? DateTimeOffset.UtcNow.AddDays(-30),
        LastLoginAt = lastLoginAt,
        IsTerminated = isTerminated,
    };

    private static AccessReviewRecord CreateReview(
        AccessReviewStatus status = AccessReviewStatus.Pending) => new()
    {
        ReviewId = "AR-2026-03-0001",
        TenantId = "tenant-1",
        ReviewPeriod = "2026-03",
        GeneratedAt = DateTimeOffset.UtcNow,
        Status = status,
        TotalAssignments = 5,
        Findings = [],
    };

    // ── GenerateReview ────────────────────────────────────────────────────

    [Fact]
    public void GenerateReview_ValidInput_ReturnsSuccessWithPendingStatus()
    {
        var assignments = new List<RoleAssignmentEntry>
        {
            CreateAssignment(lastLoginAt: DateTimeOffset.UtcNow.AddDays(-1)),
        };

        var result = _service.GenerateReview("tenant-1", "2026-03", assignments);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(AccessReviewStatus.Pending);
        result.Value.TenantId.Should().Be("tenant-1");
        result.Value.ReviewPeriod.Should().Be("2026-03");
        result.Value.ReviewId.Should().StartWith("AR-2026-03-");
        result.Value.TotalAssignments.Should().Be(1);
    }

    [Fact]
    public void GenerateReview_EmptyTenantId_ReturnsFailure()
    {
        var result = _service.GenerateReview("", "2026-03", []);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    [Fact]
    public void GenerateReview_EmptyPeriod_ReturnsFailure()
    {
        var result = _service.GenerateReview("tenant-1", "", []);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    [Fact]
    public void GenerateReview_DetectsStaleAssignment_NoLoginEver()
    {
        var assignments = new List<RoleAssignmentEntry>
        {
            CreateAssignment(lastLoginAt: null),
        };

        var result = _service.GenerateReview("tenant-1", "2026-03", assignments);

        result.IsSuccess.Should().BeTrue();
        result.Value.Findings.Should().ContainSingle(f => f.FindingType == "NO_RECENT_LOGIN");
    }

    [Fact]
    public void GenerateReview_DetectsStaleAssignment_LoginOver90DaysAgo()
    {
        var assignments = new List<RoleAssignmentEntry>
        {
            CreateAssignment(lastLoginAt: DateTimeOffset.UtcNow.AddDays(-91)),
        };

        var result = _service.GenerateReview("tenant-1", "2026-03", assignments);

        result.IsSuccess.Should().BeTrue();
        result.Value.Findings.Should().Contain(f => f.FindingType == "NO_RECENT_LOGIN");
    }

    [Fact]
    public void GenerateReview_NoFinding_RecentLogin()
    {
        var assignments = new List<RoleAssignmentEntry>
        {
            CreateAssignment(lastLoginAt: DateTimeOffset.UtcNow.AddDays(-10)),
        };

        var result = _service.GenerateReview("tenant-1", "2026-03", assignments);

        result.IsSuccess.Should().BeTrue();
        result.Value.Findings.Should().BeEmpty();
    }

    [Fact]
    public void GenerateReview_DetectsElevatedPrivilege()
    {
        var assignments = new List<RoleAssignmentEntry>
        {
            CreateAssignment(roleName: "Director", lastLoginAt: DateTimeOffset.UtcNow.AddDays(-1)),
        };

        var result = _service.GenerateReview("tenant-1", "2026-03", assignments);

        result.IsSuccess.Should().BeTrue();
        result.Value.Findings.Should().Contain(f => f.FindingType == "ELEVATED_PRIVILEGE");
    }

    [Fact]
    public void GenerateReview_DetectsTerminatedWithActiveRole()
    {
        var assignments = new List<RoleAssignmentEntry>
        {
            CreateAssignment(isTerminated: true, lastLoginAt: DateTimeOffset.UtcNow.AddDays(-1)),
        };

        var result = _service.GenerateReview("tenant-1", "2026-03", assignments);

        result.IsSuccess.Should().BeTrue();
        result.Value.Findings.Should().ContainSingle(f => f.FindingType == "TERMINATED_WITH_ACTIVE_ROLE");
    }

    [Fact]
    public void GenerateReview_TerminatedTakesPrecedence_NoStaleOrElevatedFindings()
    {
        var assignments = new List<RoleAssignmentEntry>
        {
            CreateAssignment(roleName: "Director", isTerminated: true, lastLoginAt: null),
        };

        var result = _service.GenerateReview("tenant-1", "2026-03", assignments);

        result.IsSuccess.Should().BeTrue();
        result.Value.Findings.Should().HaveCount(1);
        result.Value.Findings[0].FindingType.Should().Be("TERMINATED_WITH_ACTIVE_ROLE");
    }

    [Fact]
    public void GenerateReview_MultipleAssignments_DetectsMultipleFindings()
    {
        var assignments = new List<RoleAssignmentEntry>
        {
            CreateAssignment("emp-001", "Employee", lastLoginAt: null),
            CreateAssignment("emp-002", "HRManager", lastLoginAt: DateTimeOffset.UtcNow.AddDays(-1)),
            CreateAssignment("emp-003", "Employee", isTerminated: true, lastLoginAt: DateTimeOffset.UtcNow),
        };

        var result = _service.GenerateReview("tenant-1", "2026-03", assignments);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAssignments.Should().Be(3);
        result.Value.Findings.Should().Contain(f => f.FindingType == "NO_RECENT_LOGIN" && f.EmployeeId == "emp-001");
        result.Value.Findings.Should().Contain(f => f.FindingType == "ELEVATED_PRIVILEGE" && f.EmployeeId == "emp-002");
        result.Value.Findings.Should().Contain(f => f.FindingType == "TERMINATED_WITH_ACTIVE_ROLE" && f.EmployeeId == "emp-003");
    }

    [Fact]
    public void GenerateReview_EmptyAssignments_ReturnsSuccessWithNoFindings()
    {
        var result = _service.GenerateReview("tenant-1", "2026-03", []);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAssignments.Should().Be(0);
        result.Value.Findings.Should().BeEmpty();
    }

    // ── ApproveReview ─────────────────────────────────────────────────────

    [Fact]
    public void ApproveReview_FromPending_Succeeds()
    {
        var review = CreateReview(AccessReviewStatus.Pending);
        var now = DateTimeOffset.UtcNow;

        var result = _service.ApproveReview(review, "director-001", now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(AccessReviewStatus.Approved);
        result.Value.ReviewedBy.Should().Be("director-001");
        result.Value.ReviewedAt.Should().Be(now);
    }

    [Fact]
    public void ApproveReview_FromInReview_Succeeds()
    {
        var review = CreateReview(AccessReviewStatus.InReview);
        var now = DateTimeOffset.UtcNow;

        var result = _service.ApproveReview(review, "director-001", now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(AccessReviewStatus.Approved);
    }

    [Fact]
    public void ApproveReview_FromApproved_ReturnsFailure()
    {
        var review = CreateReview(AccessReviewStatus.Approved);

        var result = _service.ApproveReview(review, "director-001", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.InvalidAccessReviewStatusTransition);
    }

    [Fact]
    public void ApproveReview_FromRejected_ReturnsFailure()
    {
        var review = CreateReview(AccessReviewStatus.Rejected);

        var result = _service.ApproveReview(review, "director-001", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.InvalidAccessReviewStatusTransition);
    }

    [Fact]
    public void ApproveReview_EmptyReviewedBy_ReturnsFailure()
    {
        var review = CreateReview(AccessReviewStatus.Pending);

        var result = _service.ApproveReview(review, "", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    // ── RejectReview ──────────────────────────────────────────────────────

    [Fact]
    public void RejectReview_FromPending_Succeeds()
    {
        var review = CreateReview(AccessReviewStatus.Pending);
        var now = DateTimeOffset.UtcNow;

        var result = _service.RejectReview(review, "director-001", "Findings need further investigation.", now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(AccessReviewStatus.Rejected);
        result.Value.ReviewedBy.Should().Be("director-001");
        result.Value.ReviewedAt.Should().Be(now);
    }

    [Fact]
    public void RejectReview_FromInReview_Succeeds()
    {
        var review = CreateReview(AccessReviewStatus.InReview);

        var result = _service.RejectReview(review, "director-001", "Incomplete data.", DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(AccessReviewStatus.Rejected);
    }

    [Fact]
    public void RejectReview_FromApproved_ReturnsFailure()
    {
        var review = CreateReview(AccessReviewStatus.Approved);

        var result = _service.RejectReview(review, "director-001", "Reason.", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.InvalidAccessReviewStatusTransition);
    }

    [Fact]
    public void RejectReview_EmptyReviewedBy_ReturnsFailure()
    {
        var review = CreateReview(AccessReviewStatus.Pending);

        var result = _service.RejectReview(review, "", "Reason.", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    [Fact]
    public void RejectReview_EmptyReason_ReturnsFailure()
    {
        var review = CreateReview(AccessReviewStatus.Pending);

        var result = _service.RejectReview(review, "director-001", "", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    // ── Edge cases ────────────────────────────────────────────────────────

    [Fact]
    public void GenerateReview_StaleAndElevated_BothDetected()
    {
        var assignments = new List<RoleAssignmentEntry>
        {
            CreateAssignment(roleName: "SaasAdmin", lastLoginAt: null),
        };

        var result = _service.GenerateReview("tenant-1", "2026-03", assignments);

        result.IsSuccess.Should().BeTrue();
        result.Value.Findings.Should().Contain(f => f.FindingType == "NO_RECENT_LOGIN");
        result.Value.Findings.Should().Contain(f => f.FindingType == "ELEVATED_PRIVILEGE");
        result.Value.Findings.Should().HaveCount(2);
    }

    [Fact]
    public void GenerateReview_LoginExactly90DaysAgo_IsNotStale()
    {
        // Use -89 days to ensure we are within the 90-day window even after
        // the small elapsed time between constructing the date and evaluating
        // the threshold inside the service (which also calls DateTimeOffset.UtcNow).
        var assignments = new List<RoleAssignmentEntry>
        {
            CreateAssignment(lastLoginAt: DateTimeOffset.UtcNow.AddDays(-89).AddHours(-23)),
        };

        var result = _service.GenerateReview("tenant-1", "2026-03", assignments);

        result.IsSuccess.Should().BeTrue();
        result.Value.Findings.Should().NotContain(f => f.FindingType == "NO_RECENT_LOGIN");
    }
}
