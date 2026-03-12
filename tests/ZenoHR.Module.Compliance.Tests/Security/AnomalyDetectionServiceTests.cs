// CTL-POPIA-008: Tests for breach detection and anomaly monitoring service.

using FluentAssertions;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Models;
using ZenoHR.Module.Compliance.Services;

namespace ZenoHR.Module.Compliance.Tests.Security;

public sealed class AnomalyDetectionServiceTests
{
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan BusinessStart = TimeSpan.FromHours(7);
    private static readonly TimeSpan BusinessEnd = TimeSpan.FromHours(18);

    // ── DetectBruteForce ──────────────────────────────────────────────────

    [Fact]
    public void DetectBruteForce_FiveFailuresInWindow_ReturnsIncident()
    {
        var now = DateTimeOffset.UtcNow;
        var events = CreateFailedAuthEvents("user-001", now, count: 5, intervalSeconds: 60);

        var result = AnomalyDetectionService.DetectBruteForce(events, DefaultWindow, threshold: 5);

        result.IsSuccess.Should().BeTrue();
        result.Value.IncidentType.Should().Be(SecurityIncidentType.BruteForceAttempt);
        result.Value.Severity.Should().Be(BreachSeverity.Medium);
        result.Value.AffectedUserId.Should().Be("user-001");
        result.Value.Status.Should().Be(IncidentStatus.Detected);
    }

    [Fact]
    public void DetectBruteForce_FourFailures_ReturnsNoIncident()
    {
        var now = DateTimeOffset.UtcNow;
        var events = CreateFailedAuthEvents("user-001", now, count: 4, intervalSeconds: 60);

        var result = AnomalyDetectionService.DetectBruteForce(events, DefaultWindow, threshold: 5);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.NoAnomalyDetected);
    }

    [Fact]
    public void DetectBruteForce_FailuresOutsideWindow_ReturnsNoIncident()
    {
        var now = DateTimeOffset.UtcNow;
        // 5 failures spread over 20 minutes — only last few within 10-min window
        var events = CreateFailedAuthEvents("user-001", now, count: 5, intervalSeconds: 300);

        var result = AnomalyDetectionService.DetectBruteForce(events, DefaultWindow, threshold: 5);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void DetectBruteForce_EmptyEvents_ReturnsNoIncident()
    {
        var result = AnomalyDetectionService.DetectBruteForce([], DefaultWindow, threshold: 5);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void DetectBruteForce_SixFailures_StillTriggersIncident()
    {
        var now = DateTimeOffset.UtcNow;
        var events = CreateFailedAuthEvents("user-002", now, count: 6, intervalSeconds: 30);

        var result = AnomalyDetectionService.DetectBruteForce(events, DefaultWindow, threshold: 5);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Contain("6");
    }

    [Fact]
    public void DetectBruteForce_MixedSuccessAndFailure_OnlyCountsFailures()
    {
        var now = DateTimeOffset.UtcNow;
        var events = new List<AuditEntry>();
        for (var i = 0; i < 10; i++)
        {
            events.Add(new AuditEntry(
                $"evt-{i}",
                "auth.login",
                "user-001",
                now.AddSeconds(-(10 - i) * 30),
                IsSuccess: i % 2 == 0)); // 5 failures, 5 successes
        }

        var result = AnomalyDetectionService.DetectBruteForce(events, DefaultWindow, threshold: 5);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void DetectBruteForce_IncidentIdFormat_StartsWithInc()
    {
        var now = DateTimeOffset.UtcNow;
        var events = CreateFailedAuthEvents("user-001", now, count: 5, intervalSeconds: 60);

        var result = AnomalyDetectionService.DetectBruteForce(events, DefaultWindow, threshold: 5);

        result.IsSuccess.Should().BeTrue();
        result.Value.IncidentId.Should().StartWith("INC-");
    }

    // ── DetectBulkExport ──────────────────────────────────────────────────

    [Fact]
    public void DetectBulkExport_OverThreshold_ReturnsIncident()
    {
        var now = DateTimeOffset.UtcNow;
        var events = CreateExportEvents("user-001", now, count: 51, intervalSeconds: 5);

        var result = AnomalyDetectionService.DetectBulkExport(events, threshold: 50);

        result.IsSuccess.Should().BeTrue();
        result.Value.IncidentType.Should().Be(SecurityIncidentType.BulkDataExport);
        result.Value.Severity.Should().Be(BreachSeverity.Medium);
        result.Value.AffectedUserId.Should().Be("user-001");
    }

    [Fact]
    public void DetectBulkExport_AtThreshold_ReturnsNoIncident()
    {
        var now = DateTimeOffset.UtcNow;
        var events = CreateExportEvents("user-001", now, count: 50, intervalSeconds: 5);

        var result = AnomalyDetectionService.DetectBulkExport(events, threshold: 50);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.NoAnomalyDetected);
    }

    [Fact]
    public void DetectBulkExport_EmptyEvents_ReturnsNoIncident()
    {
        var result = AnomalyDetectionService.DetectBulkExport([], threshold: 50);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void DetectBulkExport_EventsOutsideFiveMinuteWindow_NotCounted()
    {
        var now = DateTimeOffset.UtcNow;
        // 60 events but spread over 10 minutes — only ~30 in last 5 min window
        var events = CreateExportEvents("user-001", now, count: 60, intervalSeconds: 10);

        var result = AnomalyDetectionService.DetectBulkExport(events, threshold: 50);

        result.IsFailure.Should().BeTrue();
    }

    // ── DetectOffHoursAccess ──────────────────────────────────────────────

    [Fact]
    public void DetectOffHoursAccess_At2AM_ReturnsIncident()
    {
        var timestamp = new DateTimeOffset(2026, 3, 12, 2, 0, 0, TimeSpan.FromHours(2)); // 2AM SAST

        var result = AnomalyDetectionService.DetectOffHoursAccess("payroll.finalize", timestamp, BusinessStart, BusinessEnd);

        result.IsSuccess.Should().BeTrue();
        result.Value.IncidentType.Should().Be(SecurityIncidentType.OffHoursAccess);
        result.Value.Severity.Should().Be(BreachSeverity.Low);
        result.Value.Status.Should().Be(IncidentStatus.Detected);
    }

    [Fact]
    public void DetectOffHoursAccess_At10AM_ReturnsNoIncident()
    {
        var timestamp = new DateTimeOffset(2026, 3, 12, 10, 0, 0, TimeSpan.FromHours(2)); // 10AM SAST

        var result = AnomalyDetectionService.DetectOffHoursAccess("payroll.finalize", timestamp, BusinessStart, BusinessEnd);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.NoAnomalyDetected);
    }

    [Fact]
    public void DetectOffHoursAccess_At7AM_IsWithinBusinessHours()
    {
        var timestamp = new DateTimeOffset(2026, 3, 12, 7, 0, 0, TimeSpan.FromHours(2));

        var result = AnomalyDetectionService.DetectOffHoursAccess("payroll.finalize", timestamp, BusinessStart, BusinessEnd);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void DetectOffHoursAccess_At6PM_IsOutsideBusinessHours()
    {
        var timestamp = new DateTimeOffset(2026, 3, 12, 18, 0, 0, TimeSpan.FromHours(2)); // 6PM = boundary

        var result = AnomalyDetectionService.DetectOffHoursAccess("payroll.finalize", timestamp, BusinessStart, BusinessEnd);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void DetectOffHoursAccess_At1159PM_ReturnsIncident()
    {
        var timestamp = new DateTimeOffset(2026, 3, 12, 23, 59, 0, TimeSpan.FromHours(2));

        var result = AnomalyDetectionService.DetectOffHoursAccess("sars.approve", timestamp, BusinessStart, BusinessEnd);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Contain("sars.approve");
    }

    // ── UpdateStatus ──────────────────────────────────────────────────────

    [Fact]
    public void UpdateStatus_DetectedToInvestigating_Succeeds()
    {
        var incident = CreateTestIncident(IncidentStatus.Detected);

        var result = AnomalyDetectionService.UpdateStatus(incident, IncidentStatus.Investigating, "admin-001", DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(IncidentStatus.Investigating);
        result.Value.ResolvedBy.Should().Be("admin-001");
    }

    [Fact]
    public void UpdateStatus_InvestigatingToContained_Succeeds()
    {
        var incident = CreateTestIncident(IncidentStatus.Investigating);

        var result = AnomalyDetectionService.UpdateStatus(incident, IncidentStatus.Contained, "admin-001", DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(IncidentStatus.Contained);
    }

    [Fact]
    public void UpdateStatus_ContainedToResolved_Succeeds()
    {
        var incident = CreateTestIncident(IncidentStatus.Contained);
        var now = DateTimeOffset.UtcNow;

        var result = AnomalyDetectionService.UpdateStatus(incident, IncidentStatus.Resolved, "admin-001", now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(IncidentStatus.Resolved);
        result.Value.ResolvedAt.Should().Be(now);
    }

    [Fact]
    public void UpdateStatus_BackwardTransition_ReturnsFailure()
    {
        var incident = CreateTestIncident(IncidentStatus.Contained);

        var result = AnomalyDetectionService.UpdateStatus(incident, IncidentStatus.Detected, "admin-001", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.InvalidIncidentStatusTransition);
    }

    [Fact]
    public void UpdateStatus_ResolvedToAnything_ReturnsFailure()
    {
        var incident = CreateTestIncident(IncidentStatus.Resolved);

        var result = AnomalyDetectionService.UpdateStatus(incident, IncidentStatus.Investigating, "admin-001", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.InvalidIncidentStatusTransition);
    }

    [Fact]
    public void UpdateStatus_DetectedToFalsePositive_Succeeds()
    {
        var incident = CreateTestIncident(IncidentStatus.Detected);
        var now = DateTimeOffset.UtcNow;

        var result = AnomalyDetectionService.UpdateStatus(incident, IncidentStatus.FalsePositive, "admin-001", now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(IncidentStatus.FalsePositive);
        result.Value.ResolvedAt.Should().Be(now);
        result.Value.Notes.Should().Contain("false positive");
    }

    [Fact]
    public void UpdateStatus_InvestigatingToFalsePositive_Succeeds()
    {
        var incident = CreateTestIncident(IncidentStatus.Investigating);

        var result = AnomalyDetectionService.UpdateStatus(incident, IncidentStatus.FalsePositive, "admin-001", DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(IncidentStatus.FalsePositive);
    }

    [Fact]
    public void UpdateStatus_FalsePositiveToAnything_ReturnsFailure()
    {
        var incident = CreateTestIncident(IncidentStatus.FalsePositive);

        var result = AnomalyDetectionService.UpdateStatus(incident, IncidentStatus.Investigating, "admin-001", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static SecurityIncident CreateTestIncident(IncidentStatus status) =>
        new()
        {
            IncidentId = "INC-20260312-test0001",
            TenantId = "tenant-1",
            DetectedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            Severity = BreachSeverity.Medium,
            IncidentType = SecurityIncidentType.BruteForceAttempt,
            Description = "Test incident",
            AffectedUserId = "user-001",
            Status = status,
        };

    private static List<AuditEntry> CreateFailedAuthEvents(
        string userId, DateTimeOffset endTime, int count, int intervalSeconds)
    {
        var events = new List<AuditEntry>(count);
        for (var i = 0; i < count; i++)
        {
            events.Add(new AuditEntry(
                $"evt-{i}",
                "auth.login",
                userId,
                endTime.AddSeconds(-(count - 1 - i) * intervalSeconds),
                IsSuccess: false));
        }
        return events;
    }

    private static List<AuditEntry> CreateExportEvents(
        string userId, DateTimeOffset endTime, int count, int intervalSeconds)
    {
        var events = new List<AuditEntry>(count);
        for (var i = 0; i < count; i++)
        {
            events.Add(new AuditEntry(
                $"export-{i}",
                "data.export",
                userId,
                endTime.AddSeconds(-(count - 1 - i) * intervalSeconds),
                IsSuccess: true));
        }
        return events;
    }
}
