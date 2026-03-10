// REQ-COMP-001, CTL-SARS-006: Unit tests for ComplianceSubmission entity.
// TASK-091: Tests cover factory validation, state machine transitions, and guard clauses.
// Naming: MethodName_Scenario_ExpectedResult (xUnit + FluentAssertions + NSubstitute).

using FluentAssertions;
using ZenoHR.Domain.Common;
using ZenoHR.Module.Compliance.Entities;
using ZenoHR.Module.Compliance.Enums;

namespace ZenoHR.Module.Compliance.Tests;

/// <summary>
/// Unit tests for the <see cref="ComplianceSubmission"/> entity.
/// REQ-COMP-001: Validates factory creation rules and Pending→Submitted→Accepted/Rejected transitions.
/// </summary>
public sealed class ComplianceSubmissionTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ComplianceSubmission CreateValid(
        string tenantId = "tenant-001",
        string period = "2026-02",
        ComplianceSubmissionType type = ComplianceSubmissionType.Emp201)
    {
        var result = ComplianceSubmission.Create(
            id: $"cs_{tenantId}_{period}_emp201",
            tenantId: tenantId,
            period: period,
            submissionType: type,
            payeAmount: new MoneyZAR(11_350.00m),
            uifAmount: new MoneyZAR(354.24m),
            sdlAmount: new MoneyZAR(735.00m),
            grossAmount: new MoneyZAR(73_500.00m),
            employeeCount: 2,
            checksumSha256: "abc123",
            generatedFileContent: [1, 2, 3],
            complianceFlags: null,
            createdBy: "uid-hrmanager",
            createdAt: DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue("test helper must produce a valid submission");
        return result.Value;
    }

    // ── Create — success ─────────────────────────────────────────────────────

    /// <summary>REQ-COMP-001: Valid inputs produce a Pending submission with correct initial state.</summary>
    [Fact]
    public void Create_ValidInputs_ReturnsSuccess()
    {
        // Act
        var result = ComplianceSubmission.Create(
            id: "cs_t1_2026-02_emp201",
            tenantId: "tenant-001",
            period: "2026-02",
            submissionType: ComplianceSubmissionType.Emp201,
            payeAmount: new MoneyZAR(8_250.00m),
            uifAmount: new MoneyZAR(354.24m),
            sdlAmount: new MoneyZAR(450.00m),
            grossAmount: new MoneyZAR(45_000.00m),
            employeeCount: 1,
            checksumSha256: "sha256-abc",
            generatedFileContent: null,
            complianceFlags: null,
            createdBy: "uid-hrm",
            createdAt: DateTimeOffset.UtcNow);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TenantId.Should().Be("tenant-001");
        result.Value.Period.Should().Be("2026-02");
        result.Value.SubmissionType.Should().Be(ComplianceSubmissionType.Emp201);
        result.Value.Status.Should().Be(ComplianceSubmissionStatus.Pending);
        result.Value.FilingReference.Should().BeNull();
        result.Value.SubmittedAt.Should().BeNull();
        result.Value.AcceptedAt.Should().BeNull();
        result.Value.EmployeeCount.Should().Be(1);
        result.Value.PayeAmount.Amount.Should().Be(8_250.00m);
    }

    // ── Create — validation failures ─────────────────────────────────────────

    /// <summary>REQ-COMP-001: Empty TenantId must be rejected.</summary>
    [Fact]
    public void Create_EmptyTenantId_ReturnsFailure()
    {
        var result = ComplianceSubmission.Create(
            id: "cs_id",
            tenantId: "",
            period: "2026-02",
            submissionType: ComplianceSubmissionType.Emp201,
            payeAmount: MoneyZAR.Zero,
            uifAmount: MoneyZAR.Zero,
            sdlAmount: MoneyZAR.Zero,
            grossAmount: MoneyZAR.Zero,
            employeeCount: 0,
            checksumSha256: null,
            generatedFileContent: null,
            complianceFlags: null,
            createdBy: "uid",
            createdAt: DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("TenantId");
    }

    /// <summary>REQ-COMP-001: Whitespace-only TenantId must be rejected.</summary>
    [Fact]
    public void Create_WhitespaceTenantId_ReturnsFailure()
    {
        var result = ComplianceSubmission.Create(
            id: "cs_id",
            tenantId: "   ",
            period: "2026-02",
            submissionType: ComplianceSubmissionType.Emp201,
            payeAmount: MoneyZAR.Zero,
            uifAmount: MoneyZAR.Zero,
            sdlAmount: MoneyZAR.Zero,
            grossAmount: MoneyZAR.Zero,
            employeeCount: 0,
            checksumSha256: null,
            generatedFileContent: null,
            complianceFlags: null,
            createdBy: "uid",
            createdAt: DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("TenantId");
    }

    /// <summary>REQ-COMP-001: Empty Period must be rejected.</summary>
    [Fact]
    public void Create_EmptyPeriod_ReturnsFailure()
    {
        var result = ComplianceSubmission.Create(
            id: "cs_id",
            tenantId: "tenant-001",
            period: "",
            submissionType: ComplianceSubmissionType.Emp201,
            payeAmount: MoneyZAR.Zero,
            uifAmount: MoneyZAR.Zero,
            sdlAmount: MoneyZAR.Zero,
            grossAmount: MoneyZAR.Zero,
            employeeCount: 0,
            checksumSha256: null,
            generatedFileContent: null,
            complianceFlags: null,
            createdBy: "uid",
            createdAt: DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Period");
    }

    // ── MarkSubmitted ─────────────────────────────────────────────────────────

    /// <summary>REQ-COMP-001: Pending → Submitted is a valid transition.</summary>
    [Fact]
    public void MarkSubmitted_FromPending_TransitionSucceeds()
    {
        var submission = CreateValid();

        var result = submission.MarkSubmitted("SARS-REF-2026-001", DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        submission.Status.Should().Be(ComplianceSubmissionStatus.Submitted);
    }

    /// <summary>REQ-COMP-001: MarkSubmitted sets FilingReference and SubmittedAt.</summary>
    [Fact]
    public void MarkSubmitted_WithFilingReference_SetsReference()
    {
        var submission = CreateValid();
        var now = new DateTimeOffset(2026, 3, 7, 0, 0, 0, TimeSpan.Zero);
        var reference = "SARS-REF-7654321";

        submission.MarkSubmitted(reference, now);

        submission.FilingReference.Should().Be(reference);
        submission.SubmittedAt.Should().Be(now);
    }

    /// <summary>REQ-COMP-001: Cannot submit an already-Accepted submission.</summary>
    [Fact]
    public void MarkSubmitted_FromAccepted_ReturnsFailure()
    {
        var submission = CreateValid();
        var now = DateTimeOffset.UtcNow;
        submission.MarkSubmitted("REF-001", now);
        submission.MarkAccepted(now);

        // Attempt a second transition from Accepted back to Submitted
        var result = submission.MarkSubmitted("REF-002", now);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Accepted");
    }

    /// <summary>REQ-COMP-001: MarkSubmitted with empty reference must fail.</summary>
    [Fact]
    public void MarkSubmitted_EmptyFilingReference_ReturnsFailure()
    {
        var submission = CreateValid();

        var result = submission.MarkSubmitted("", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("FilingReference");
    }

    // ── MarkAccepted ──────────────────────────────────────────────────────────

    /// <summary>REQ-COMP-001: Submitted → Accepted is a valid transition.</summary>
    [Fact]
    public void MarkAccepted_FromSubmitted_TransitionSucceeds()
    {
        var submission = CreateValid();
        var now = DateTimeOffset.UtcNow;
        submission.MarkSubmitted("REF-001", now);

        var result = submission.MarkAccepted(now.AddMinutes(5));

        result.IsSuccess.Should().BeTrue();
        submission.Status.Should().Be(ComplianceSubmissionStatus.Accepted);
        submission.AcceptedAt.Should().Be(now.AddMinutes(5));
    }

    /// <summary>REQ-COMP-001: Cannot accept a Pending (not-yet-submitted) submission.</summary>
    [Fact]
    public void MarkAccepted_FromPending_ReturnsFailure()
    {
        var submission = CreateValid();

        var result = submission.MarkAccepted(DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Pending");
    }

    // ── MarkRejected ──────────────────────────────────────────────────────────

    /// <summary>REQ-COMP-001: Submitted → Rejected appends rejection reason to ComplianceFlags.</summary>
    [Fact]
    public void MarkRejected_FromSubmitted_AddsReasonToFlags()
    {
        var submission = CreateValid();
        var now = DateTimeOffset.UtcNow;
        submission.MarkSubmitted("REF-001", now);

        var result = submission.MarkRejected("Invalid PAYE reference number", now.AddMinutes(10));

        result.IsSuccess.Should().BeTrue();
        submission.Status.Should().Be(ComplianceSubmissionStatus.Rejected);
        submission.ComplianceFlags.Should().ContainMatch("*REJECTED*");
        submission.ComplianceFlags.Should().ContainMatch("*Invalid PAYE reference number*");
    }

    /// <summary>REQ-COMP-001: Cannot reject a Pending (not-yet-submitted) submission.</summary>
    [Fact]
    public void MarkRejected_FromPending_ReturnsFailure()
    {
        var submission = CreateValid();

        var result = submission.MarkRejected("Some reason", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Pending");
    }

    /// <summary>REQ-COMP-001: Empty rejection reason must fail.</summary>
    [Fact]
    public void MarkRejected_EmptyReason_ReturnsFailure()
    {
        var submission = CreateValid();
        var now = DateTimeOffset.UtcNow;
        submission.MarkSubmitted("REF-001", now);

        var result = submission.MarkRejected("", now);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("reason");
    }

    // ── Reconstitute ─────────────────────────────────────────────────────────

    /// <summary>REQ-COMP-001: Reconstitution hydrates all fields faithfully.</summary>
    [Fact]
    public void Reconstitute_AllFields_HydratesCorrectly()
    {
        var id = "cs_t1_2026-02_emp201";
        var tenantId = "tenant-001";
        var period = "2026-02";
        var createdAt = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var flags = new List<string> { "REJECTED:Bad reference" }.AsReadOnly();

        var submission = ComplianceSubmission.Reconstitute(
            id: id,
            tenantId: tenantId,
            period: period,
            submissionType: ComplianceSubmissionType.Emp201,
            status: ComplianceSubmissionStatus.Rejected,
            filingReference: "REF-XYZ",
            submittedAt: createdAt.AddHours(1),
            acceptedAt: null,
            payeAmount: new MoneyZAR(8_250m),
            uifAmount: new MoneyZAR(354.24m),
            sdlAmount: new MoneyZAR(450m),
            grossAmount: new MoneyZAR(45_000m),
            employeeCount: 2,
            checksumSha256: "sha256xyz",
            generatedFileContent: null,
            complianceFlags: flags,
            createdAt: createdAt,
            createdBy: "uid-test",
            schemaVersion: "1.0");

        submission.Id.Should().Be(id);
        submission.TenantId.Should().Be(tenantId);
        submission.Period.Should().Be(period);
        submission.Status.Should().Be(ComplianceSubmissionStatus.Rejected);
        submission.FilingReference.Should().Be("REF-XYZ");
        submission.ComplianceFlags.Should().ContainSingle("REJECTED:Bad reference");
        submission.PayeAmount.Amount.Should().Be(8_250m);
        submission.EmployeeCount.Should().Be(2);
        submission.SchemaVersion.Should().Be("1.0");
    }

    // ── EMP501 type ───────────────────────────────────────────────────────────

    /// <summary>REQ-COMP-002: EMP501 annual submission can be created with period "2026".</summary>
    [Fact]
    public void Create_Emp501AnnualType_ReturnsSuccess()
    {
        var result = ComplianceSubmission.Create(
            id: "cs_t1_2026_emp501",
            tenantId: "tenant-001",
            period: "2026",
            submissionType: ComplianceSubmissionType.Emp501,
            payeAmount: new MoneyZAR(99_000m),
            uifAmount: new MoneyZAR(4_250m),
            sdlAmount: new MoneyZAR(5_400m),
            grossAmount: new MoneyZAR(540_000m),
            employeeCount: 10,
            checksumSha256: null,
            generatedFileContent: null,
            complianceFlags: null,
            createdBy: "uid-hrm",
            createdAt: DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        result.Value.SubmissionType.Should().Be(ComplianceSubmissionType.Emp501);
        result.Value.Period.Should().Be("2026");
    }
}
