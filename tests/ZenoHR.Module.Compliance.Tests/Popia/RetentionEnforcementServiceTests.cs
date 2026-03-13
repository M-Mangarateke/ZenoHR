// CTL-POPIA-015: Tests for retention enforcement service and retention policy.
// POPIA §14 — verifies correct identification of expired records and workflow transitions.

using FluentAssertions;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Services.RetentionEnforcement;

namespace ZenoHR.Module.Compliance.Tests.Popia;

public sealed class RetentionEnforcementServiceTests
{
    private readonly RetentionEnforcementService _sut = new();

    // ── RetentionPolicy.IsRetentionExpired ─────────────────────────────────

    [Fact]
    public void IsRetentionExpired_ExactlyAtExpiry_ReturnsTrue()
    {
        var termination = new DateTimeOffset(2020, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var current = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero);

        RetentionPolicy.IsRetentionExpired(termination, RetentionPolicy.PopiaDefaultRetentionYears, current)
            .Should().BeTrue("5 years have elapsed for a 5-year retention period");
    }

    [Fact]
    public void IsRetentionExpired_OneDayBefore_ReturnsFalse()
    {
        var termination = new DateTimeOffset(2020, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var current = new DateTimeOffset(2025, 6, 14, 0, 0, 0, TimeSpan.Zero);

        RetentionPolicy.IsRetentionExpired(termination, RetentionPolicy.PopiaDefaultRetentionYears, current)
            .Should().BeFalse("one day remains in the retention period");
    }

    [Fact]
    public void IsRetentionExpired_PayrollThreeYears_ReturnsTrue()
    {
        var termination = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var current = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        RetentionPolicy.IsRetentionExpired(termination, RetentionPolicy.BceaPayrollRetentionYears, current)
            .Should().BeTrue("3 years have elapsed for BCEA payroll retention");
    }

    [Fact]
    public void IsRetentionExpired_AuditTrailSevenYears_ReturnsFalse()
    {
        var termination = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var current = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero);

        RetentionPolicy.IsRetentionExpired(termination, RetentionPolicy.AuditTrailRetentionYears, current)
            .Should().BeFalse("only ~7 years have not yet elapsed for 7-year audit retention");
    }

    // ── RetentionPolicy.GetRetentionExpiryDate ────────────────────────────

    [Fact]
    public void GetRetentionExpiryDate_ReturnsCorrectDate()
    {
        var termination = new DateTimeOffset(2021, 7, 20, 0, 0, 0, TimeSpan.Zero);

        var expiry = RetentionPolicy.GetRetentionExpiryDate(termination, 5);

        expiry.Should().Be(new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void GetRetentionExpiryDate_LeapYear_HandledCorrectly()
    {
        var termination = new DateTimeOffset(2020, 2, 29, 0, 0, 0, TimeSpan.Zero);

        var expiry = RetentionPolicy.GetRetentionExpiryDate(termination, 5);

        // 2025 is not a leap year; Feb 29 + 5 years = Feb 28
        expiry.Should().Be(new DateTimeOffset(2025, 2, 28, 0, 0, 0, TimeSpan.Zero));
    }

    // ── RetentionPolicy.DetermineRetentionYears ───────────────────────────

    [Theory]
    [InlineData(DataCategory.Payroll, 3)]
    [InlineData(DataCategory.General, 5)]
    [InlineData(DataCategory.Leave, 5)]
    [InlineData(DataCategory.TimeAttendance, 5)]
    [InlineData(DataCategory.AuditTrail, 7)]
    [InlineData(DataCategory.ComplianceSubmission, 7)]
    public void DetermineRetentionYears_ReturnsCorrectPeriod(DataCategory category, int expected)
    {
        RetentionPolicy.DetermineRetentionYears(category).Should().Be(expected);
    }

    [Fact]
    public void DetermineRetentionYears_Unknown_ThrowsArgumentOutOfRange()
    {
        var act = () => RetentionPolicy.DetermineRetentionYears(DataCategory.Unknown);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("dataCategory");
    }

    // ── RetentionPolicy Constants ─────────────────────────────────────────

    [Fact]
    public void BceaPayrollRetentionYears_IsThree()
    {
        RetentionPolicy.BceaPayrollRetentionYears.Should().Be(3);
    }

    [Fact]
    public void PopiaDefaultRetentionYears_IsFive()
    {
        RetentionPolicy.PopiaDefaultRetentionYears.Should().Be(5);
    }

    [Fact]
    public void AuditTrailRetentionYears_IsSeven()
    {
        RetentionPolicy.AuditTrailRetentionYears.Should().Be(7);
    }

    // ── IdentifyExpiredRecords ─────────────────────────────────────────────

    [Fact]
    public void IdentifyExpiredRecords_TerminatedPastRetention_ReturnsReview()
    {
        var termination = new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var current = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero); // 6.5 years later

        var employees = new[]
        {
            ("tenant-1", "emp-001", termination, new[] { DataCategory.General } as IEnumerable<DataCategory>)
        };

        var results = _sut.IdentifyExpiredRecords(employees, current);

        results.Should().HaveCount(1);
        results[0].TenantId.Should().Be("tenant-1");
        results[0].EmployeeId.Should().Be("emp-001");
        results[0].DataCategory.Should().Be(DataCategory.General);
        results[0].Status.Should().Be(RetentionReviewStatus.Pending);
    }

    [Fact]
    public void IdentifyExpiredRecords_NotYetExpired_ReturnsEmpty()
    {
        var termination = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var current = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero); // Only 2.5 years

        var employees = new[]
        {
            ("tenant-1", "emp-001", termination, new[] { DataCategory.General } as IEnumerable<DataCategory>)
        };

        var results = _sut.IdentifyExpiredRecords(employees, current);

        results.Should().BeEmpty("retention period has not elapsed");
    }

    [Fact]
    public void IdentifyExpiredRecords_MultipleCategories_ReturnsOnlyExpired()
    {
        var termination = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var current = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero); // 3.5 years

        var employees = new[]
        {
            ("tenant-1", "emp-001", termination,
                new[] { DataCategory.Payroll, DataCategory.General, DataCategory.AuditTrail } as IEnumerable<DataCategory>)
        };

        var results = _sut.IdentifyExpiredRecords(employees, current);

        // Payroll (3yr) should be expired, General (5yr) and AuditTrail (7yr) should not
        results.Should().HaveCount(1);
        results[0].DataCategory.Should().Be(DataCategory.Payroll);
    }

    [Fact]
    public void IdentifyExpiredRecords_EmptyInput_ReturnsEmpty()
    {
        var results = _sut.IdentifyExpiredRecords(
            Array.Empty<(string, string, DateTimeOffset, IEnumerable<DataCategory>)>(),
            DateTimeOffset.UtcNow);

        results.Should().BeEmpty();
    }

    [Fact]
    public void IdentifyExpiredRecords_ReviewIdContainsTenantAndEmployee()
    {
        var termination = new DateTimeOffset(2018, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var current = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var employees = new[]
        {
            ("tenant-abc", "emp-xyz", termination, new[] { DataCategory.General } as IEnumerable<DataCategory>)
        };

        var results = _sut.IdentifyExpiredRecords(employees, current);

        results[0].ReviewId.Should().Contain("tenant-abc");
        results[0].ReviewId.Should().Contain("emp-xyz");
    }

    // ── ApproveAnonymisation ──────────────────────────────────────────────

    [Fact]
    public void ApproveAnonymisation_PendingReview_TransitionsToApproved()
    {
        var review = CreatePendingReview();

        var result = _sut.ApproveAnonymisation(review, "hr-manager@zenowethu.co.za");

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(RetentionReviewStatus.Approved);
        result.Value.ReviewedBy.Should().Be("hr-manager@zenowethu.co.za");
        result.Value.ReviewedAt.Should().NotBeNull();
    }

    [Fact]
    public void ApproveAnonymisation_AlreadyApproved_ReturnsFailure()
    {
        var review = CreatePendingReview() with { Status = RetentionReviewStatus.Approved };

        var result = _sut.ApproveAnonymisation(review, "hr-manager@zenowethu.co.za");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ComplianceCheckFailed);
    }

    // ── MarkAnonymised ────────────────────────────────────────────────────

    [Fact]
    public void MarkAnonymised_ApprovedReview_TransitionsToAnonymised()
    {
        var review = CreatePendingReview() with { Status = RetentionReviewStatus.Approved };

        var result = _sut.MarkAnonymised(review);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(RetentionReviewStatus.Anonymised);
    }

    [Fact]
    public void MarkAnonymised_PendingReview_ReturnsFailure()
    {
        var review = CreatePendingReview();

        var result = _sut.MarkAnonymised(review);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ComplianceCheckFailed);
    }

    // ── ExtendRetention ───────────────────────────────────────────────────

    [Fact]
    public void ExtendRetention_PendingReview_TransitionsToRetained()
    {
        var review = CreatePendingReview();

        var result = _sut.ExtendRetention(review, "Pending litigation — case REF-2025-001", "director@zenowethu.co.za");

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(RetentionReviewStatus.Retained);
        result.Value.RetentionReason.Should().Contain("Pending litigation");
        result.Value.ReviewedBy.Should().Be("director@zenowethu.co.za");
    }

    [Fact]
    public void ExtendRetention_AlreadyAnonymised_ReturnsFailure()
    {
        var review = CreatePendingReview() with { Status = RetentionReviewStatus.Anonymised };

        var result = _sut.ExtendRetention(review, "Legal hold", "director@zenowethu.co.za");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ComplianceCheckFailed);
    }

    // ── RetentionReviewStatus enum ────────────────────────────────────────

    [Fact]
    public void RetentionReviewStatus_UnknownIsZero()
    {
        ((int)RetentionReviewStatus.Unknown).Should().Be(0);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static RetentionReviewRecord CreatePendingReview() =>
        new(
            ReviewId: "RET-tenant1-emp001-General",
            TenantId: "tenant1",
            EmployeeId: "emp001",
            DataCategory: DataCategory.General,
            TerminationDate: new DateTimeOffset(2019, 6, 15, 0, 0, 0, TimeSpan.Zero),
            RetentionExpiryDate: new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero),
            Status: RetentionReviewStatus.Pending);
}
