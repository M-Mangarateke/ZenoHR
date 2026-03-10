// REQ-COMP-001, CTL-SARS-006: Firestore repository for the compliance_submissions collection.
// TASK-091: Persistence for ComplianceSubmission entity.
// Monetary fields stored as strings — docs/schemas/monetary-precision.md.
// Tenant isolation: every read and query filters by tenant_id — REQ-SEC-005.

using System.Globalization;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Entities;
using ZenoHR.Module.Compliance.Enums;

namespace ZenoHR.Infrastructure.Firestore;

/// <summary>
/// Firestore repository for the <c>compliance_submissions</c> root collection.
/// REQ-COMP-001: Persists ComplianceSubmission across Pending → Submitted → Accepted/Rejected lifecycle.
/// REQ-SEC-005: All reads and queries filter by tenant_id to enforce tenant isolation.
/// </summary>
public sealed class ComplianceSubmissionRepository : BaseFirestoreRepository<ComplianceSubmission>
{
    public ComplianceSubmissionRepository(
        FirestoreDb db,
        ILogger<ComplianceSubmissionRepository> logger) : base(db, logger) { }

    protected override string CollectionName => "compliance_submissions";
    protected override ZenoHrErrorCode NotFoundErrorCode => ZenoHrErrorCode.ComplianceSubmissionNotFound;

    // ── Reads ────────────────────────────────────────────────────────────────

    /// <summary>Gets a submission by document ID, verifying tenant ownership.</summary>
    public new async Task<Result<ComplianceSubmission?>> GetByIdAsync(
        string tenantId, string id, CancellationToken ct = default)
    {
        var result = await base.GetByIdAsync(tenantId, id, ct);
        if (result.IsFailure)
        {
            // Distinguish "not found" from other errors — return null on not-found
            if (result.Error.Code == ZenoHrErrorCode.ComplianceSubmissionNotFound)
                return Result<ComplianceSubmission?>.Success(null);
            return Result<ComplianceSubmission?>.Failure(result.Error);
        }
        return Result<ComplianceSubmission?>.Success(result.Value);
    }

    /// <summary>
    /// Lists all compliance submissions for a tenant, newest first.
    /// REQ-COMP-001: Returns up to <paramref name="limit"/> submissions.
    /// </summary>
    public async Task<Result<IReadOnlyList<ComplianceSubmission>>> ListByTenantAsync(
        string tenantId, int limit = 50, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId)
            .OrderByDescending("created_at")
            .Limit(limit);

        var results = await ExecuteQueryAsync(query, ct);
        return Result<IReadOnlyList<ComplianceSubmission>>.Success(results);
    }

    /// <summary>
    /// Lists compliance submissions for a specific filing period.
    /// REQ-COMP-001: "YYYY-MM" for monthly, "YYYY" for annual.
    /// </summary>
    public async Task<Result<IReadOnlyList<ComplianceSubmission>>> ListByPeriodAsync(
        string tenantId, string period, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId)
            .WhereEqualTo("period", period)
            .OrderByDescending("created_at");

        var results = await ExecuteQueryAsync(query, ct);
        return Result<IReadOnlyList<ComplianceSubmission>>.Success(results);
    }

    // ── Writes ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Persists a new <see cref="ComplianceSubmission"/> to Firestore.
    /// REQ-COMP-001: Uses SetAsync (upsert) — idempotent for re-generation scenarios.
    /// </summary>
    public async Task<Result<ComplianceSubmission>> CreateAsync(
        ComplianceSubmission submission, CancellationToken ct = default)
    {
        var saveResult = await SetDocumentAsync(submission.Id, submission, ct);
        if (saveResult.IsFailure)
            return Result<ComplianceSubmission>.Failure(saveResult.Error);

        return Result<ComplianceSubmission>.Success(submission);
    }

    /// <summary>
    /// Updates the status fields of an existing submission (after MarkSubmitted/MarkAccepted/MarkRejected).
    /// REQ-COMP-001: Only status-related fields are updated — amounts and file content are immutable.
    /// </summary>
    public async Task<Result<ComplianceSubmission>> UpdateStatusAsync(
        ComplianceSubmission submission, CancellationToken ct = default)
    {
        var docRef = Collection.Document(submission.Id);

        // REQ-SEC-005: Verify tenant before updating
        var snapshot = await docRef.GetSnapshotAsync(ct);
        if (!snapshot.Exists)
            return Result<ComplianceSubmission>.Failure(
                ZenoHrError.NotFound(ZenoHrErrorCode.ComplianceSubmissionNotFound,
                    CollectionName, submission.Id));

        if (snapshot.TryGetValue<string>("tenant_id", out var snapshotTenantId)
            && !string.Equals(snapshotTenantId, submission.TenantId, StringComparison.Ordinal))
            return Result<ComplianceSubmission>.Failure(
                ZenoHrError.NotFound(ZenoHrErrorCode.ComplianceSubmissionNotFound,
                    CollectionName, submission.Id));

        // Update status-related fields only
        var updates = new Dictionary<string, object?>
        {
            ["status"] = ToStatusString(submission.Status),
            ["filing_reference"] = submission.FilingReference,
            ["submitted_at"] = submission.SubmittedAt.HasValue
                ? Timestamp.FromDateTimeOffset(submission.SubmittedAt.Value)
                : (object?)null,
            ["accepted_at"] = submission.AcceptedAt.HasValue
                ? Timestamp.FromDateTimeOffset(submission.AcceptedAt.Value)
                : (object?)null,
            ["compliance_flags"] = submission.ComplianceFlags.ToList(),
            ["updated_at"] = Timestamp.GetCurrentTimestamp(),
        };

#pragma warning disable CA2016 // Firestore UpdateAsync does not accept CancellationToken
        await docRef.UpdateAsync(updates!, Precondition.None);
#pragma warning restore CA2016
        return Result<ComplianceSubmission>.Success(submission);
    }

    // ── Hydration ────────────────────────────────────────────────────────────

    protected override ComplianceSubmission FromSnapshot(DocumentSnapshot snapshot)
    {
        var submissionType = ParseSubmissionType(
            snapshot.TryGetValue<string>("submission_type", out var st) ? st : "");

        var status = ParseStatus(
            snapshot.TryGetValue<string>("status", out var s) ? s : "");

        string? filingReference = null;
        snapshot.TryGetValue("filing_reference", out filingReference);

        DateTimeOffset? submittedAt = null;
        if (snapshot.TryGetValue<Timestamp>("submitted_at", out var subTs))
            submittedAt = subTs.ToDateTimeOffset();

        DateTimeOffset? acceptedAt = null;
        if (snapshot.TryGetValue<Timestamp>("accepted_at", out var accTs))
            acceptedAt = accTs.ToDateTimeOffset();

        IReadOnlyList<string> complianceFlags = [];
        if (snapshot.TryGetValue<List<object>>("compliance_flags", out var flagObjects))
            complianceFlags = flagObjects.Select(o => o?.ToString() ?? "").ToList();

        // Decode base64 file content if present
        byte[]? fileContent = null;
        if (snapshot.TryGetValue<string>("generated_file_content", out var b64)
            && !string.IsNullOrWhiteSpace(b64))
        {
            try { fileContent = Convert.FromBase64String(b64); }
            catch (FormatException) { /* corrupt data — treat as null */ }
        }

        int employeeCount = 0;
        if (snapshot.TryGetValue<long>("employee_count", out var empCount))
            employeeCount = (int)empCount;

        return ComplianceSubmission.Reconstitute(
            id: snapshot.Id,
            tenantId: snapshot.GetValue<string>("tenant_id"),
            period: snapshot.GetValue<string>("period"),
            submissionType: submissionType,
            status: status,
            filingReference: filingReference,
            submittedAt: submittedAt,
            acceptedAt: acceptedAt,
            payeAmount: ReadMoney(snapshot, "paye_amount"),
            uifAmount: ReadMoney(snapshot, "uif_amount"),
            sdlAmount: ReadMoney(snapshot, "sdl_amount"),
            grossAmount: ReadMoney(snapshot, "gross_amount"),
            employeeCount: employeeCount,
            checksumSha256: snapshot.TryGetValue<string>("checksum_sha256", out var chk) ? chk : null,
            generatedFileContent: fileContent,
            complianceFlags: complianceFlags,
            createdAt: snapshot.GetValue<Timestamp>("created_at").ToDateTimeOffset(),
            createdBy: snapshot.GetValue<string>("created_by"),
            schemaVersion: snapshot.TryGetValue<string>("schema_version", out var sv) ? sv : "1.0");
    }

    // ── Serialisation ─────────────────────────────────────────────────────────

    protected override Dictionary<string, object?> ToDocument(ComplianceSubmission s) => new()
    {
        ["id"] = s.Id,
        ["tenant_id"] = s.TenantId,
        ["period"] = s.Period,
        ["submission_type"] = ToSubmissionTypeString(s.SubmissionType),
        ["status"] = ToStatusString(s.Status),
        ["filing_reference"] = s.FilingReference,
        ["submitted_at"] = s.SubmittedAt.HasValue
            ? Timestamp.FromDateTimeOffset(s.SubmittedAt.Value)
            : (object?)null,
        ["accepted_at"] = s.AcceptedAt.HasValue
            ? Timestamp.FromDateTimeOffset(s.AcceptedAt.Value)
            : (object?)null,
        // REQ-COMP-001: Monetary fields stored as strings — docs/schemas/monetary-precision.md
        ["paye_amount"] = s.PayeAmount.ToFirestoreString(),
        ["uif_amount"] = s.UifAmount.ToFirestoreString(),
        ["sdl_amount"] = s.SdlAmount.ToFirestoreString(),
        ["gross_amount"] = s.GrossAmount.ToFirestoreString(),
        ["employee_count"] = s.EmployeeCount,
        ["checksum_sha256"] = s.ChecksumSha256,
        // Store binary file content as base64 string in Firestore
        ["generated_file_content"] = s.GeneratedFileContent != null
            ? Convert.ToBase64String(s.GeneratedFileContent)
            : null,
        ["compliance_flags"] = s.ComplianceFlags.ToList(),
        ["created_at"] = Timestamp.FromDateTimeOffset(s.CreatedAt),
        ["created_by"] = s.CreatedBy,
        ["updated_at"] = Timestamp.GetCurrentTimestamp(),
        ["schema_version"] = s.SchemaVersion,
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MoneyZAR ReadMoney(DocumentSnapshot snapshot, string field)
    {
        if (snapshot.TryGetValue<string>(field, out var str) && !string.IsNullOrWhiteSpace(str))
            return MoneyZAR.FromFirestoreString(str);
        return MoneyZAR.Zero;
    }

    private static string ToSubmissionTypeString(ComplianceSubmissionType t) => t switch
    {
        ComplianceSubmissionType.Emp201 => "Emp201",
        ComplianceSubmissionType.Emp501 => "Emp501",
        _ => "Unknown",
    };

    private static ComplianceSubmissionType ParseSubmissionType(string v) => v switch
    {
        "Emp201" => ComplianceSubmissionType.Emp201,
        "Emp501" => ComplianceSubmissionType.Emp501,
        _ => ComplianceSubmissionType.Unknown,
    };

    private static string ToStatusString(ComplianceSubmissionStatus s) => s switch
    {
        ComplianceSubmissionStatus.Pending => "Pending",
        ComplianceSubmissionStatus.Submitted => "Submitted",
        ComplianceSubmissionStatus.Accepted => "Accepted",
        ComplianceSubmissionStatus.Rejected => "Rejected",
        _ => "Unknown",
    };

    private static ComplianceSubmissionStatus ParseStatus(string v) => v switch
    {
        "Pending" => ComplianceSubmissionStatus.Pending,
        "Submitted" => ComplianceSubmissionStatus.Submitted,
        "Accepted" => ComplianceSubmissionStatus.Accepted,
        "Rejected" => ComplianceSubmissionStatus.Rejected,
        _ => ComplianceSubmissionStatus.Unknown,
    };
}
