// REQ-COMP-001, CTL-SARS-006: ComplianceSubmission entity — tracks one SARS filing event.
// TASK-091: ComplianceSubmission entity + lifecycle state machine.
// State machine: Pending → Submitted → Accepted | Rejected.
// Immutability: Accepted/Rejected are terminal — no further transitions permitted.

using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Enums;

namespace ZenoHR.Module.Compliance.Entities;

/// <summary>
/// Tracks a single SARS compliance filing event (EMP201 monthly or EMP501 annual).
/// REQ-COMP-001: Compliance submissions flow Pending → Submitted → Accepted | Rejected.
/// CTL-SARS-006: Filing reference number and timestamps are captured at each state transition.
/// </summary>
public sealed class ComplianceSubmission
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Firestore document ID — format: cs_{tenantId}_{period}_{type}.</summary>
    public string Id { get; private init; } = string.Empty;

    /// <summary>Tenant isolation key. REQ-SEC-005: every document scoped to a tenant.</summary>
    public string TenantId { get; private init; } = string.Empty;

    // ── Descriptors ───────────────────────────────────────────────────────────

    /// <summary>"YYYY-MM" for EMP201 (monthly), "YYYY" for EMP501 (annual).</summary>
    public string Period { get; private init; } = string.Empty;

    /// <summary>Whether this is an EMP201 or EMP501 submission.</summary>
    public ComplianceSubmissionType SubmissionType { get; private init; }

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>Current lifecycle status of the submission.</summary>
    public ComplianceSubmissionStatus Status { get; private set; }

    /// <summary>SARS eFiling reference number — set when <see cref="MarkSubmitted"/> is called.</summary>
    public string? FilingReference { get; private set; }

    /// <summary>UTC timestamp when the filing was exported/submitted to SARS.</summary>
    public DateTimeOffset? SubmittedAt { get; private set; }

    /// <summary>UTC timestamp when SARS confirmed acceptance.</summary>
    public DateTimeOffset? AcceptedAt { get; private set; }

    // ── Declared Amounts ─────────────────────────────────────────────────────

    /// <summary>Total PAYE declared in this submission. REQ-COMP-001: MoneyZAR — no float.</summary>
    public MoneyZAR PayeAmount { get; private init; }

    /// <summary>Total UIF declared in this submission.</summary>
    public MoneyZAR UifAmount { get; private init; }

    /// <summary>Total SDL declared in this submission.</summary>
    public MoneyZAR SdlAmount { get; private init; }

    /// <summary>Total gross remuneration declared in this submission.</summary>
    public MoneyZAR GrossAmount { get; private init; }

    /// <summary>Number of employees covered by this submission.</summary>
    public int EmployeeCount { get; private init; }

    // ── File Content ─────────────────────────────────────────────────────────

    /// <summary>SHA-256 hex of the generated CSV/XML content — integrity check.</summary>
    public string? ChecksumSha256 { get; private init; }

    /// <summary>The actual CSV/XML bytes for download. Stored as base64 string in Firestore.</summary>
    public byte[]? GeneratedFileContent { get; private init; }

    // ── Compliance ────────────────────────────────────────────────────────────

    /// <summary>Compliance flags or rejection reasons attached to this submission.</summary>
    public IReadOnlyList<string> ComplianceFlags { get; private set; } = [];

    // ── Audit ─────────────────────────────────────────────────────────────────

    /// <summary>UTC timestamp when this submission document was created.</summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>Firebase UID of the user who initiated the generation.</summary>
    public string CreatedBy { get; private init; } = string.Empty;

    /// <summary>Schema version for forward-compatibility.</summary>
    public string SchemaVersion { get; private init; } = "1.0";

    // ── Private constructor (use factory) ─────────────────────────────────────

    private ComplianceSubmission() { }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="ComplianceSubmission"/> in the <see cref="ComplianceSubmissionStatus.Pending"/> state.
    /// REQ-COMP-001: Validates required fields and initialises monetary amounts.
    /// </summary>
    public static Result<ComplianceSubmission> Create(
        string id,
        string tenantId,
        string period,
        ComplianceSubmissionType submissionType,
        MoneyZAR payeAmount,
        MoneyZAR uifAmount,
        MoneyZAR sdlAmount,
        MoneyZAR grossAmount,
        int employeeCount,
        string? checksumSha256,
        byte[]? generatedFileContent,
        IReadOnlyList<string>? complianceFlags,
        string createdBy,
        DateTimeOffset createdAt)
    {
        // REQ-COMP-001: Required field validation
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<ComplianceSubmission>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing, "TenantId is required.");

        if (string.IsNullOrWhiteSpace(period))
            return Result<ComplianceSubmission>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing, "Period is required.");

        if (string.IsNullOrWhiteSpace(createdBy))
            return Result<ComplianceSubmission>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing, "CreatedBy is required.");

        if (submissionType == ComplianceSubmissionType.Unknown)
            return Result<ComplianceSubmission>.Failure(
                ZenoHrErrorCode.ValidationFailed, "SubmissionType must be Emp201 or Emp501.");

        if (employeeCount < 0)
            return Result<ComplianceSubmission>.Failure(
                ZenoHrErrorCode.ValueOutOfRange, "EmployeeCount must be zero or positive.");

        var submission = new ComplianceSubmission
        {
            Id = string.IsNullOrWhiteSpace(id)
                ? $"cs_{tenantId}_{period}_{submissionType.ToString().ToLowerInvariant()}"
                : id,
            TenantId = tenantId,
            Period = period,
            SubmissionType = submissionType,
            Status = ComplianceSubmissionStatus.Pending,
            PayeAmount = payeAmount,
            UifAmount = uifAmount,
            SdlAmount = sdlAmount,
            GrossAmount = grossAmount,
            EmployeeCount = employeeCount,
            ChecksumSha256 = checksumSha256,
            GeneratedFileContent = generatedFileContent,
            ComplianceFlags = complianceFlags ?? [],
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            SchemaVersion = "1.0",
        };

        return Result<ComplianceSubmission>.Success(submission);
    }

    // ── Reconstitution (Firestore hydration — bypasses validation) ────────────

    /// <summary>
    /// Reconstitutes a <see cref="ComplianceSubmission"/> from persisted Firestore fields.
    /// Must only be called by the repository layer.
    /// </summary>
    public static ComplianceSubmission Reconstitute(
        string id,
        string tenantId,
        string period,
        ComplianceSubmissionType submissionType,
        ComplianceSubmissionStatus status,
        string? filingReference,
        DateTimeOffset? submittedAt,
        DateTimeOffset? acceptedAt,
        MoneyZAR payeAmount,
        MoneyZAR uifAmount,
        MoneyZAR sdlAmount,
        MoneyZAR grossAmount,
        int employeeCount,
        string? checksumSha256,
        byte[]? generatedFileContent,
        IReadOnlyList<string> complianceFlags,
        DateTimeOffset createdAt,
        string createdBy,
        string schemaVersion)
    {
        return new ComplianceSubmission
        {
            Id = id,
            TenantId = tenantId,
            Period = period,
            SubmissionType = submissionType,
            Status = status,
            FilingReference = filingReference,
            SubmittedAt = submittedAt,
            AcceptedAt = acceptedAt,
            PayeAmount = payeAmount,
            UifAmount = uifAmount,
            SdlAmount = sdlAmount,
            GrossAmount = grossAmount,
            EmployeeCount = employeeCount,
            ChecksumSha256 = checksumSha256,
            GeneratedFileContent = generatedFileContent,
            ComplianceFlags = complianceFlags,
            CreatedAt = createdAt,
            CreatedBy = createdBy,
            SchemaVersion = schemaVersion,
        };
    }

    // ── State Transitions ─────────────────────────────────────────────────────

    /// <summary>
    /// Transitions the submission from <see cref="ComplianceSubmissionStatus.Pending"/>
    /// to <see cref="ComplianceSubmissionStatus.Submitted"/> and records the SARS filing reference.
    /// REQ-COMP-001: FilingReference is mandatory at submission time.
    /// </summary>
    public Result<ComplianceSubmission> MarkSubmitted(string filingReference, DateTimeOffset now)
    {
        if (Status != ComplianceSubmissionStatus.Pending)
            return Result<ComplianceSubmission>.Failure(
                ZenoHrErrorCode.ValidationFailed,
                $"Cannot mark as Submitted from status {Status}. Must be Pending.");

        if (string.IsNullOrWhiteSpace(filingReference))
            return Result<ComplianceSubmission>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing, "FilingReference is required when submitting.");

        Status = ComplianceSubmissionStatus.Submitted;
        FilingReference = filingReference;
        SubmittedAt = now;

        return Result<ComplianceSubmission>.Success(this);
    }

    /// <summary>
    /// Transitions the submission from <see cref="ComplianceSubmissionStatus.Submitted"/>
    /// to <see cref="ComplianceSubmissionStatus.Accepted"/>.
    /// CTL-SARS-006: Records SARS acceptance timestamp.
    /// </summary>
    public Result<ComplianceSubmission> MarkAccepted(DateTimeOffset now)
    {
        if (Status != ComplianceSubmissionStatus.Submitted)
            return Result<ComplianceSubmission>.Failure(
                ZenoHrErrorCode.ValidationFailed,
                $"Cannot mark as Accepted from status {Status}. Must be Submitted.");

        Status = ComplianceSubmissionStatus.Accepted;
        AcceptedAt = now;

        return Result<ComplianceSubmission>.Success(this);
    }

    /// <summary>
    /// Transitions the submission from <see cref="ComplianceSubmissionStatus.Submitted"/>
    /// to <see cref="ComplianceSubmissionStatus.Rejected"/> and appends the rejection reason
    /// to <see cref="ComplianceFlags"/>.
    /// REQ-COMP-001: Rejection reason is mandatory and stored for audit purposes.
    /// </summary>
    public Result<ComplianceSubmission> MarkRejected(string reason, DateTimeOffset now)
    {
        if (Status != ComplianceSubmissionStatus.Submitted)
            return Result<ComplianceSubmission>.Failure(
                ZenoHrErrorCode.ValidationFailed,
                $"Cannot mark as Rejected from status {Status}. Must be Submitted.");

        if (string.IsNullOrWhiteSpace(reason))
            return Result<ComplianceSubmission>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing, "Rejection reason is required.");

        Status = ComplianceSubmissionStatus.Rejected;
        var flags = new List<string>(ComplianceFlags) { $"REJECTED:{reason}" };
        ComplianceFlags = flags.AsReadOnly();

        return Result<ComplianceSubmission>.Success(this);
    }
}
