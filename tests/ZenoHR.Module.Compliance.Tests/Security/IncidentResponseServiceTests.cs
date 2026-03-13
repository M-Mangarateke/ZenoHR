// CTL-POPIA-012, REQ-SEC-009: Tests for incident response & containment lifecycle.

using FluentAssertions;
using ZenoHR.Module.Compliance.Models;
using ZenoHR.Module.Compliance.Services;

namespace ZenoHR.Module.Compliance.Tests.Security;

public sealed class IncidentResponseServiceTests
{
    private readonly IncidentResponseService _service = new();
    private static readonly DateTimeOffset Now = new(2026, 3, 13, 10, 0, 0, TimeSpan.Zero);

    // ── ClassifyIncident ───────────────────────────────────────────────────

    [Fact]
    public void ClassifyIncident_ValidInput_CreatesRecord()
    {
        var result = _service.ClassifyIncident(
            "tenant-1", "SI-2026-0001", IncidentSeverityLevel.Sev1Critical, "user-001", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(IncidentResponseStatus.Classified);
        result.Value.IncidentId.Should().StartWith("IR-");
        result.Value.TenantId.Should().Be("tenant-1");
        result.Value.SecurityIncidentId.Should().Be("SI-2026-0001");
        result.Value.Severity.Should().Be(IncidentSeverityLevel.Sev1Critical);
        result.Value.ClassifiedBy.Should().Be("user-001");
        result.Value.ClassifiedAt.Should().Be(Now);
        result.Value.ContainmentActions.Should().BeEmpty();
        result.Value.EvidencePackGenerated.Should().BeFalse();
    }

    [Fact]
    public void ClassifyIncident_EmptyTenantId_ReturnsFailure()
    {
        var result = _service.ClassifyIncident(
            "", "SI-2026-0001", IncidentSeverityLevel.Sev2High, "user-001", Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ClassifyIncident_EmptySecurityIncidentId_ReturnsFailure()
    {
        var result = _service.ClassifyIncident(
            "tenant-1", "", IncidentSeverityLevel.Sev2High, "user-001", Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ClassifyIncident_EmptyClassifiedBy_ReturnsFailure()
    {
        var result = _service.ClassifyIncident(
            "tenant-1", "SI-2026-0001", IncidentSeverityLevel.Sev2High, "", Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ClassifyIncident_UnknownSeverity_ReturnsFailure()
    {
        var result = _service.ClassifyIncident(
            "tenant-1", "SI-2026-0001", IncidentSeverityLevel.Unknown, "user-001", Now);

        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(IncidentSeverityLevel.Sev1Critical)]
    [InlineData(IncidentSeverityLevel.Sev2High)]
    [InlineData(IncidentSeverityLevel.Sev3Medium)]
    [InlineData(IncidentSeverityLevel.Sev4Low)]
    public void ClassifyIncident_EachSeverityLevel_Accepted(IncidentSeverityLevel severity)
    {
        var result = _service.ClassifyIncident(
            "tenant-1", "SI-2026-0001", severity, "user-001", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Severity.Should().Be(severity);
    }

    [Fact]
    public void ClassifyIncident_MultipleCalls_GeneratesUniqueIds()
    {
        var result1 = _service.ClassifyIncident(
            "tenant-1", "SI-2026-0001", IncidentSeverityLevel.Sev1Critical, "user-001", Now);
        var result2 = _service.ClassifyIncident(
            "tenant-1", "SI-2026-0002", IncidentSeverityLevel.Sev2High, "user-002", Now);

        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result1.Value.IncidentId.Should().NotBe(result2.Value.IncidentId);
    }

    // ── RecordContainment ──────────────────────────────────────────────────

    [Fact]
    public void RecordContainment_FromClassified_AddsActionAndTransitions()
    {
        var incident = CreateClassifiedIncident();
        var action = CreateContainmentAction();

        var result = _service.RecordContainment(incident, action);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(IncidentResponseStatus.Contained);
        result.Value.ContainmentActions.Should().HaveCount(1);
        result.Value.ContainmentActions[0].ActionId.Should().Be("CA-001");
    }

    [Fact]
    public void RecordContainment_MultipleActions_AllPresent()
    {
        var incident = CreateClassifiedIncident();
        var action1 = CreateContainmentAction("CA-001", ContainmentActionType.DisableAccount);
        var action2 = CreateContainmentAction("CA-002", ContainmentActionType.FreezePayrollRun);

        var result1 = _service.RecordContainment(incident, action1);
        result1.IsSuccess.Should().BeTrue();

        var result2 = _service.RecordContainment(result1.Value, action2);
        result2.IsSuccess.Should().BeTrue();
        result2.Value.ContainmentActions.Should().HaveCount(2);
        result2.Value.ContainmentActions[0].ActionId.Should().Be("CA-001");
        result2.Value.ContainmentActions[1].ActionId.Should().Be("CA-002");
    }

    [Fact]
    public void RecordContainment_FromInvestigating_ReturnsFailure()
    {
        var incident = CreateClassifiedIncident() with { Status = IncidentResponseStatus.Investigating };
        var action = CreateContainmentAction();

        var result = _service.RecordContainment(incident, action);

        result.IsFailure.Should().BeTrue();
    }

    // ── StartInvestigation ─────────────────────────────────────────────────

    [Fact]
    public void StartInvestigation_FromContained_Succeeds()
    {
        var incident = CreateClassifiedIncident() with { Status = IncidentResponseStatus.Contained };

        var result = _service.StartInvestigation(incident, "investigator-001");

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(IncidentResponseStatus.Investigating);
        result.Value.InvestigationNotes.Should().Contain("investigator-001");
    }

    [Fact]
    public void StartInvestigation_FromClassified_ReturnsFailure()
    {
        var incident = CreateClassifiedIncident();

        var result = _service.StartInvestigation(incident, "investigator-001");

        result.IsFailure.Should().BeTrue();
    }

    // ── RecordRecovery ─────────────────────────────────────────────────────

    [Fact]
    public void RecordRecovery_FromInvestigating_Succeeds()
    {
        var incident = CreateClassifiedIncident() with { Status = IncidentResponseStatus.Investigating };

        var result = _service.RecordRecovery(incident, "recovery-001", "Systems restored.", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(IncidentResponseStatus.Recovered);
        result.Value.RecoveredBy.Should().Be("recovery-001");
        result.Value.RecoveredAt.Should().Be(Now);
    }

    [Fact]
    public void RecordRecovery_FromContained_ReturnsFailure()
    {
        var incident = CreateClassifiedIncident() with { Status = IncidentResponseStatus.Contained };

        var result = _service.RecordRecovery(incident, "recovery-001", "Notes", Now);

        result.IsFailure.Should().BeTrue();
    }

    // ── SubmitPostReview ───────────────────────────────────────────────────

    [Fact]
    public void SubmitPostReview_FromRecovered_Succeeds()
    {
        var incident = CreateClassifiedIncident() with { Status = IncidentResponseStatus.Recovered };

        var result = _service.SubmitPostReview(incident, "Root cause identified.", "reviewer-001", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(IncidentResponseStatus.PostReview);
        result.Value.PostReviewNotes.Should().Be("Root cause identified.");
        result.Value.ReviewedBy.Should().Be("reviewer-001");
        result.Value.ReviewedAt.Should().Be(Now);
    }

    [Fact]
    public void SubmitPostReview_FromInvestigating_ReturnsFailure()
    {
        var incident = CreateClassifiedIncident() with { Status = IncidentResponseStatus.Investigating };

        var result = _service.SubmitPostReview(incident, "Notes", "reviewer-001", Now);

        result.IsFailure.Should().BeTrue();
    }

    // ── CloseIncident ──────────────────────────────────────────────────────

    [Fact]
    public void CloseIncident_FromPostReview_Succeeds()
    {
        var incident = CreateClassifiedIncident() with { Status = IncidentResponseStatus.PostReview };

        var result = _service.CloseIncident(incident, "closer-001", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(IncidentResponseStatus.Closed);
        result.Value.ClosedBy.Should().Be("closer-001");
        result.Value.ClosedAt.Should().Be(Now);
    }

    [Fact]
    public void CloseIncident_FromRecovered_ReturnsFailure()
    {
        var incident = CreateClassifiedIncident() with { Status = IncidentResponseStatus.Recovered };

        var result = _service.CloseIncident(incident, "closer-001", Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CloseIncident_FromInvestigating_ReturnsFailure()
    {
        var incident = CreateClassifiedIncident() with { Status = IncidentResponseStatus.Investigating };

        var result = _service.CloseIncident(incident, "closer-001", Now);

        result.IsFailure.Should().BeTrue();
    }

    // ── Full Lifecycle ─────────────────────────────────────────────────────

    [Fact]
    public void FullLifecycle_ClassifyContainInvestigateRecoverReviewClose_Succeeds()
    {
        // Classify
        var classifyResult = _service.ClassifyIncident(
            "tenant-1", "SI-2026-0001", IncidentSeverityLevel.Sev1Critical, "user-001", Now);
        classifyResult.IsSuccess.Should().BeTrue();
        classifyResult.Value.Status.Should().Be(IncidentResponseStatus.Classified);

        // Contain
        var containResult = _service.RecordContainment(
            classifyResult.Value, CreateContainmentAction());
        containResult.IsSuccess.Should().BeTrue();
        containResult.Value.Status.Should().Be(IncidentResponseStatus.Contained);

        // Investigate
        var investigateResult = _service.StartInvestigation(
            containResult.Value, "investigator-001");
        investigateResult.IsSuccess.Should().BeTrue();
        investigateResult.Value.Status.Should().Be(IncidentResponseStatus.Investigating);

        // Recover
        var recoverResult = _service.RecordRecovery(
            investigateResult.Value, "recovery-001", "All systems restored.", Now.AddHours(2));
        recoverResult.IsSuccess.Should().BeTrue();
        recoverResult.Value.Status.Should().Be(IncidentResponseStatus.Recovered);

        // Post-review
        var reviewResult = _service.SubmitPostReview(
            recoverResult.Value, "Root cause: misconfigured firewall rule.", "reviewer-001", Now.AddHours(4));
        reviewResult.IsSuccess.Should().BeTrue();
        reviewResult.Value.Status.Should().Be(IncidentResponseStatus.PostReview);

        // Close
        var closeResult = _service.CloseIncident(
            reviewResult.Value, "closer-001", Now.AddHours(5));
        closeResult.IsSuccess.Should().BeTrue();
        closeResult.Value.Status.Should().Be(IncidentResponseStatus.Closed);
    }

    // ── Backward Transition Rejected ───────────────────────────────────────

    [Fact]
    public void BackwardTransition_ClosedToInvestigating_ReturnsFailure()
    {
        var incident = CreateClassifiedIncident() with { Status = IncidentResponseStatus.Closed };

        var result = _service.StartInvestigation(incident, "investigator-001");

        result.IsFailure.Should().BeTrue();
    }

    // ── Skip Transition Rejected ───────────────────────────────────────────

    [Fact]
    public void SkipTransition_ClassifiedToRecovered_ReturnsFailure()
    {
        var incident = CreateClassifiedIncident();

        var result = _service.RecordRecovery(incident, "recovery-001", "Notes", Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SkipTransition_ClassifiedToClosed_ReturnsFailure()
    {
        var incident = CreateClassifiedIncident();

        var result = _service.CloseIncident(incident, "closer-001", Now);

        result.IsFailure.Should().BeTrue();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private IncidentResponse CreateClassifiedIncident()
    {
        return new IncidentResponse
        {
            IncidentId = "IR-2026-0001",
            TenantId = "tenant-1",
            SecurityIncidentId = "SI-2026-0001",
            Severity = IncidentSeverityLevel.Sev1Critical,
            Status = IncidentResponseStatus.Classified,
            ClassifiedBy = "user-001",
            ClassifiedAt = Now,
        };
    }

    private static ContainmentAction CreateContainmentAction(
        string actionId = "CA-001",
        ContainmentActionType actionType = ContainmentActionType.DisableAccount)
    {
        return new ContainmentAction
        {
            ActionId = actionId,
            ActionType = actionType,
            TargetId = "user-suspect-001",
            PerformedBy = "sec-ops-001",
            PerformedAt = Now,
            Notes = "Account disabled pending investigation.",
        };
    }
}
