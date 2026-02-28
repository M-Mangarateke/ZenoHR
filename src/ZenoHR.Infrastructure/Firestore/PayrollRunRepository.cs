// REQ-HR-003, CTL-SARS-001: Firestore repository for payroll_runs root collection.
// TASK-082: PayrollRun aggregate persistence.
// Immutability: Finalized runs are write-once (only Filed transition permitted).
// Monetary fields stored as strings — see docs/schemas/monetary-precision.md.

using Google.Cloud.Firestore;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Payroll.Aggregates;
using ZenoHR.Module.Payroll.Calculation;

namespace ZenoHR.Infrastructure.Firestore;

/// <summary>
/// Firestore repository for the <c>payroll_runs</c> root collection.
/// REQ-HR-003: Persists PayrollRun aggregate (Draft → Calculated → Finalized → Filed).
/// CTL-SARS-001: Immutability guard — refuses to overwrite a Finalized run
///               unless it is transitioning to Filed.
/// </summary>
public sealed class PayrollRunRepository : BaseFirestoreRepository<PayrollRun>
{
    public PayrollRunRepository(FirestoreDb db) : base(db) { }

    protected override string CollectionName => "payroll_runs";
    protected override ZenoHrErrorCode NotFoundErrorCode => ZenoHrErrorCode.PayrollRunNotFound;

    // ── Public reads ─────────────────────────────────────────────────────────

    /// <summary>Gets a payroll run by its document ID, verifying tenant ownership.</summary>
    public Task<Result<PayrollRun>> GetByRunIdAsync(
        string tenantId, string runId, CancellationToken ct = default)
        => GetByIdAsync(tenantId, runId, ct);

    /// <summary>Lists all payroll runs for a tenant, newest first.</summary>
    public Task<IReadOnlyList<PayrollRun>> ListByTenantAsync(
        string tenantId, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId).OrderByDescending("created_at");
        return ExecuteQueryAsync(query, ct);
    }

    /// <summary>Lists payroll runs for a specific period string (e.g. "2026-03").</summary>
    public Task<IReadOnlyList<PayrollRun>> ListByPeriodAsync(
        string tenantId, string period, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId)
            .WhereEqualTo("period", period)
            .OrderByDescending("created_at");
        return ExecuteQueryAsync(query, ct);
    }

    /// <summary>Lists runs by status (e.g. Draft, Calculated).</summary>
    public Task<IReadOnlyList<PayrollRun>> ListByStatusAsync(
        string tenantId, PayrollRunStatus status, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId)
            .WhereEqualTo("status", ToStatusString(status))
            .OrderByDescending("created_at");
        return ExecuteQueryAsync(query, ct);
    }

    // ── Writes ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Upserts a payroll run.
    /// CTL-SARS-001: Refuses to overwrite a run that is already Filed
    /// (Filed is terminal — no further writes permitted).
    /// </summary>
    public async Task<Result> SaveAsync(PayrollRun run, CancellationToken ct = default)
    {
        // Guard: Filed is terminal. Reject any attempt to overwrite.
        // Reads current status from Firestore before writing.
        var existing = await GetByIdAsync(run.TenantId, run.Id, ct);
        if (existing.IsSuccess && existing.Value!.Status == PayrollRunStatus.Filed
            && run.Status != PayrollRunStatus.Filed)
        {
            return Result.Failure(ZenoHrErrorCode.PayrollRunAlreadyFinalized,
                $"PayrollRun {run.Id} is Filed — no further writes permitted.");
        }

        return await SetDocumentAsync(run.Id, run, ct);
    }

    // ── Hydration ────────────────────────────────────────────────────────────

    protected override PayrollRun FromSnapshot(DocumentSnapshot snapshot)
    {
        var status = ParseStatus(snapshot.GetValue<string>("status"));
        var dataStatus = ParseDataStatus(snapshot.GetValue<string>("data_status"));
        var runType = ParseRunType(snapshot.GetValue<string>("run_type"));

        var taxYearValue = snapshot.TryGetValue<long>("tax_year", out var tyLong)
            ? (int)tyLong
            : 2026;

        // employee_ids array — stored as an array of strings
        IReadOnlyList<string> employeeIds = [];
        if (snapshot.TryGetValue<List<object>>("employee_ids", out var empIdObjects))
            employeeIds = empIdObjects.Select(o => o?.ToString() ?? "").ToList();

        // compliance_flags array
        IReadOnlyList<string> complianceFlags = [];
        if (snapshot.TryGetValue<List<object>>("compliance_flags", out var flagObjects))
            complianceFlags = flagObjects.Select(o => o?.ToString() ?? "").ToList();

        string? checksum = null;
        snapshot.TryGetValue("checksum", out checksum);
        string? finalizedBy = null;
        snapshot.TryGetValue("finalized_by", out finalizedBy);

        DateTimeOffset? calculatedAt = null;
        if (snapshot.TryGetValue<Timestamp>("calculated_at", out var calTs))
            calculatedAt = calTs.ToDateTimeOffset();

        DateTimeOffset? finalizedAt = null;
        if (snapshot.TryGetValue<Timestamp>("finalized_at", out var finTs))
            finalizedAt = finTs.ToDateTimeOffset();

        DateTimeOffset? filedAt = null;
        if (snapshot.TryGetValue<Timestamp>("filed_at", out var fileTs))
            filedAt = fileTs.ToDateTimeOffset();

        return PayrollRun.Reconstitute(
            id: snapshot.Id,
            tenantId: snapshot.GetValue<string>("tenant_id"),
            period: snapshot.GetValue<string>("period"),
            runType: runType,
            taxYear: new TaxYear(taxYearValue),
            ruleSetVersion: snapshot.GetValue<string>("rule_set_version"),
            employeeIds: employeeIds,
            initiatedBy: snapshot.GetValue<string>("initiated_by"),
            idempotencyKey: snapshot.GetValue<string>("idempotency_key"),
            createdAt: snapshot.GetValue<Timestamp>("created_at").ToDateTimeOffset(),
            status: status,
            dataStatus: dataStatus,
            grossTotal: ReadMoney(snapshot, "gross_total_zar"),
            payeTotal: ReadMoney(snapshot, "paye_total_zar"),
            uifTotal: ReadMoney(snapshot, "uif_total_zar"),
            sdlTotal: ReadMoney(snapshot, "sdl_total_zar"),
            etiTotal: ReadMoney(snapshot, "eti_total_zar"),
            deductionTotal: ReadMoney(snapshot, "deduction_total_zar"),
            netTotal: ReadMoney(snapshot, "net_total_zar"),
            complianceFlags: complianceFlags,
            checksum: checksum,
            calculatedAt: calculatedAt,
            finalizedBy: finalizedBy,
            finalizedAt: finalizedAt,
            filedAt: filedAt);
    }

    // ── Serialisation ────────────────────────────────────────────────────────

    protected override Dictionary<string, object?> ToDocument(PayrollRun r) => new()
    {
        ["tenant_id"] = r.TenantId,
        ["payroll_run_id"] = r.Id,
        ["period"] = r.Period,
        ["run_type"] = ToRunTypeString(r.RunType),
        ["tax_year"] = r.TaxYear.EndingYear,
        ["status"] = ToStatusString(r.Status),
        ["data_status"] = ToDataStatusString(r.DataStatus),
        ["rule_set_version"] = r.RuleSetVersion,
        ["employee_ids"] = r.EmployeeIds.ToList(),
        ["employee_count"] = r.EmployeeCount,
        ["gross_total_zar"] = r.GrossTotal.ToFirestoreString(),
        ["paye_total_zar"] = r.PayeTotal.ToFirestoreString(),
        ["uif_total_zar"] = r.UifTotal.ToFirestoreString(),
        ["sdl_total_zar"] = r.SdlTotal.ToFirestoreString(),
        ["eti_total_zar"] = r.EtiTotal.ToFirestoreString(),
        ["deduction_total_zar"] = r.DeductionTotal.ToFirestoreString(),
        ["net_total_zar"] = r.NetTotal.ToFirestoreString(),
        ["compliance_flags"] = r.ComplianceFlags.ToList(),
        ["checksum"] = r.Checksum,
        ["initiated_by"] = r.InitiatedBy,
        ["idempotency_key"] = r.IdempotencyKey,
        ["calculated_at"] = r.CalculatedAt.HasValue
            ? Timestamp.FromDateTimeOffset(r.CalculatedAt.Value)
            : (object?)null,
        ["finalized_by"] = r.FinalizedBy,
        ["finalized_at"] = r.FinalizedAt.HasValue
            ? Timestamp.FromDateTimeOffset(r.FinalizedAt.Value)
            : (object?)null,
        ["filed_at"] = r.FiledAt.HasValue
            ? Timestamp.FromDateTimeOffset(r.FiledAt.Value)
            : (object?)null,
        ["created_at"] = Timestamp.FromDateTimeOffset(r.CreatedAt),
        ["updated_at"] = Timestamp.GetCurrentTimestamp(),
        ["schema_version"] = r.SchemaVersion,
    };

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static MoneyZAR ReadMoney(DocumentSnapshot snapshot, string field)
    {
        if (snapshot.TryGetValue<string>(field, out var str) && !string.IsNullOrWhiteSpace(str))
            return MoneyZAR.FromFirestoreString(str);
        return MoneyZAR.Zero;
    }

    private static string ToStatusString(PayrollRunStatus s) => s switch
    {
        PayrollRunStatus.Draft => "Draft",
        PayrollRunStatus.Calculated => "Calculated",
        PayrollRunStatus.Finalized => "Finalized",
        PayrollRunStatus.Filed => "Filed",
        _ => "Draft",
    };

    private static PayrollRunStatus ParseStatus(string v) => v switch
    {
        "Draft" => PayrollRunStatus.Draft,
        "Calculated" => PayrollRunStatus.Calculated,
        "Finalized" => PayrollRunStatus.Finalized,
        "Filed" => PayrollRunStatus.Filed,
        _ => PayrollRunStatus.Unknown,
    };

    private static string ToDataStatusString(PayrollRunDataStatus s) => s switch
    {
        PayrollRunDataStatus.Active => "active",
        PayrollRunDataStatus.Archived => "archived",
        PayrollRunDataStatus.LegalHold => "legal_hold",
        _ => "active",
    };

    private static PayrollRunDataStatus ParseDataStatus(string v) => v switch
    {
        "active" => PayrollRunDataStatus.Active,
        "archived" => PayrollRunDataStatus.Archived,
        "legal_hold" => PayrollRunDataStatus.LegalHold,
        _ => PayrollRunDataStatus.Active,
    };

    private static string ToRunTypeString(PayFrequency f) => f switch
    {
        PayFrequency.Monthly => "monthly",
        PayFrequency.Weekly => "weekly",
        _ => "monthly",
    };

    private static PayFrequency ParseRunType(string v) => v switch
    {
        "monthly" => PayFrequency.Monthly,
        "weekly" => PayFrequency.Weekly,
        _ => PayFrequency.Unknown,
    };
}
