// CTL-POPIA-010, CTL-POPIA-011: Tests for POPIA breach notification workflow.

using FluentAssertions;
using ZenoHR.Module.Compliance.Models;
using ZenoHR.Module.Compliance.Services;

namespace ZenoHR.Module.Compliance.Tests.Popia;

public sealed class BreachNotificationServiceTests
{
    private readonly BreachNotificationService _service = new();

    private static BreachRecord CreateTestBreach(
        BreachStatus status = BreachStatus.Detected,
        DateTimeOffset? discoveredAt = null)
    {
        return new BreachRecord
        {
            BreachId = "BRE-2026-0001",
            TenantId = "tenant-1",
            Title = "Salary data exposure",
            Description = "Employee salary CSV was emailed to wrong recipient.",
            Severity = BreachSeverity.High,
            Status = status,
            DiscoveredAt = discoveredAt ?? DateTimeOffset.UtcNow,
            DiscoveredBy = "user-001",
            AffectedDataCategories = ["Salary Data", "Bank Details"],
            EstimatedAffectedSubjects = 15,
            RootCause = "Incorrect email auto-complete selection.",
            RemediationSteps = ["Recalled email", "Reset file sharing permissions", "Notified affected employees"]
        };
    }

    // ── RegisterBreach ───────────────────────────────────────────────────

    [Fact]
    public void RegisterBreach_ValidInput_ReturnsBreachRecord()
    {
        var result = _service.RegisterBreach(
            "tenant-1", "Test breach", "Description of breach",
            BreachSeverity.High, "user-001",
            ["ID Numbers", "Bank Details"], 10,
            "Phishing attack", ["Revoked credentials", "Notified users"],
            DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BreachStatus.Detected);
        result.Value.BreachId.Should().StartWith("BRE-");
        result.Value.TenantId.Should().Be("tenant-1");
        result.Value.AffectedDataCategories.Should().HaveCount(2);
    }

    [Fact]
    public void RegisterBreach_EmptyTenantId_ReturnsFailure()
    {
        var result = _service.RegisterBreach(
            "", "Title", "Desc", BreachSeverity.High, "user-001",
            ["PII"], 5, "Cause", ["Fix"], DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RegisterBreach_EmptyTitle_ReturnsFailure()
    {
        var result = _service.RegisterBreach(
            "tenant-1", "", "Desc", BreachSeverity.High, "user-001",
            ["PII"], 5, "Cause", ["Fix"], DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RegisterBreach_UnknownSeverity_ReturnsFailure()
    {
        var result = _service.RegisterBreach(
            "tenant-1", "Title", "Desc", BreachSeverity.Unknown, "user-001",
            ["PII"], 5, "Cause", ["Fix"], DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RegisterBreach_EmptyAffectedCategories_ReturnsFailure()
    {
        var result = _service.RegisterBreach(
            "tenant-1", "Title", "Desc", BreachSeverity.High, "user-001",
            [], 5, "Cause", ["Fix"], DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RegisterBreach_NegativeAffectedSubjects_ReturnsFailure()
    {
        var result = _service.RegisterBreach(
            "tenant-1", "Title", "Desc", BreachSeverity.High, "user-001",
            ["PII"], -1, "Cause", ["Fix"], DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    // ── UpdateStatus ─────────────────────────────────────────────────────

    [Fact]
    public void UpdateStatus_ForwardTransition_Succeeds()
    {
        var breach = CreateTestBreach(BreachStatus.Detected);
        var now = DateTimeOffset.UtcNow;

        var result = _service.UpdateStatus(breach, BreachStatus.Investigating, now);
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BreachStatus.Investigating);
    }

    [Fact]
    public void UpdateStatus_BackwardTransition_ReturnsFailure()
    {
        var breach = CreateTestBreach(BreachStatus.Contained);

        var result = _service.UpdateStatus(breach, BreachStatus.Detected, DateTimeOffset.UtcNow);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void UpdateStatus_ToContained_SetsContainedAt()
    {
        var breach = CreateTestBreach(BreachStatus.Investigating);
        var now = DateTimeOffset.UtcNow;

        var result = _service.UpdateStatus(breach, BreachStatus.Contained, now);
        result.IsSuccess.Should().BeTrue();
        result.Value.ContainedAt.Should().Be(now);
    }

    [Fact]
    public void UpdateStatus_ToRegulatorNotified_SetsRegulatorNotifiedAt()
    {
        var breach = CreateTestBreach(BreachStatus.NotificationPending);
        var now = DateTimeOffset.UtcNow;

        var result = _service.UpdateStatus(breach, BreachStatus.RegulatorNotified, now);
        result.IsSuccess.Should().BeTrue();
        result.Value.RegulatorNotifiedAt.Should().Be(now);
    }

    [Fact]
    public void UpdateStatus_ToSubjectsNotified_SetsSubjectsNotifiedAt()
    {
        var breach = CreateTestBreach(BreachStatus.RegulatorNotified);
        var now = DateTimeOffset.UtcNow;

        var result = _service.UpdateStatus(breach, BreachStatus.SubjectsNotified, now);
        result.IsSuccess.Should().BeTrue();
        result.Value.SubjectsNotifiedAt.Should().Be(now);
    }

    [Fact]
    public void UpdateStatus_ToClosed_SetsClosedAt()
    {
        var breach = CreateTestBreach(BreachStatus.Remediated);
        var now = DateTimeOffset.UtcNow;

        var result = _service.UpdateStatus(breach, BreachStatus.Closed, now);
        result.IsSuccess.Should().BeTrue();
        result.Value.ClosedAt.Should().Be(now);
    }

    // ── GetOverdueBreaches ───────────────────────────────────────────────

    [Fact]
    public void GetOverdueBreaches_PastDeadline_ReturnsOverdue()
    {
        var overdueBreach = CreateTestBreach(
            BreachStatus.Investigating,
            discoveredAt: DateTimeOffset.UtcNow.AddHours(-73));

        var result = _service.GetOverdueBreaches([overdueBreach]);
        result.Should().HaveCount(1);
        result[0].BreachId.Should().Be(overdueBreach.BreachId);
    }

    [Fact]
    public void GetOverdueBreaches_WithinDeadline_ReturnsEmpty()
    {
        var freshBreach = CreateTestBreach(
            BreachStatus.Detected,
            discoveredAt: DateTimeOffset.UtcNow.AddHours(-1));

        var result = _service.GetOverdueBreaches([freshBreach]);
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetOverdueBreaches_AlreadyNotified_ReturnsEmpty()
    {
        var notifiedBreach = CreateTestBreach(BreachStatus.RegulatorNotified,
            discoveredAt: DateTimeOffset.UtcNow.AddHours(-100));

        var result = _service.GetOverdueBreaches([notifiedBreach]);
        result.Should().BeEmpty();
    }

    // ── NotificationDeadline / IsOverdue ─────────────────────────────────

    [Fact]
    public void NotificationDeadline_Is72HoursAfterDiscovery()
    {
        var discovered = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
        var breach = CreateTestBreach(discoveredAt: discovered);

        breach.NotificationDeadline.Should().Be(discovered.AddHours(72));
    }

    [Fact]
    public void IsOverdue_BeforeDeadlineAndNotNotified_ReturnsFalse()
    {
        var breach = CreateTestBreach(
            BreachStatus.Investigating,
            discoveredAt: DateTimeOffset.UtcNow.AddHours(-1));

        breach.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_AfterDeadlineAndNotNotified_ReturnsTrue()
    {
        var breach = CreateTestBreach(
            BreachStatus.Investigating,
            discoveredAt: DateTimeOffset.UtcNow.AddHours(-73));

        breach.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public void IsOverdue_AfterDeadlineButNotified_ReturnsFalse()
    {
        var breach = CreateTestBreach(
            BreachStatus.RegulatorNotified,
            discoveredAt: DateTimeOffset.UtcNow.AddHours(-100));

        breach.IsOverdue.Should().BeFalse();
    }

    // ── GenerateRegulatorNotification ────────────────────────────────────

    [Fact]
    public void GenerateRegulatorNotification_ContainedBreach_ReturnsNotification()
    {
        var breach = CreateTestBreach(BreachStatus.Contained);

        var result = _service.GenerateRegulatorNotification(breach);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("POPIA SECTION 22");
    }

    [Fact]
    public void GenerateRegulatorNotification_DetectedBreach_ReturnsFailure()
    {
        var breach = CreateTestBreach(BreachStatus.Detected);

        var result = _service.GenerateRegulatorNotification(breach);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GenerateRegulatorNotification_ContainsRequiredFields()
    {
        var breach = CreateTestBreach(BreachStatus.Contained);

        var result = _service.GenerateRegulatorNotification(breach);
        result.IsSuccess.Should().BeTrue();

        var text = result.Value;
        text.Should().Contain(breach.BreachId);
        text.Should().Contain(breach.Description);
        text.Should().Contain("Salary Data");
        text.Should().Contain("Bank Details");
        text.Should().Contain("15");
        text.Should().Contain(breach.RootCause);
        text.Should().Contain("Recalled email");
        text.Should().Contain("Data Protection Officer");
    }
}
