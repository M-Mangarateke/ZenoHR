// REQ-HR-003, CTL-SARS-001: Firestore repository for payroll_adjustments root collection.
// TASK-084: Post-finalization correction persistence (append-only).
// Append-only invariant — no updates or deletes.

using Microsoft.Extensions.Logging;
using Google.Cloud.Firestore;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Payroll.Entities;

namespace ZenoHR.Infrastructure.Firestore;

/// <summary>
/// Firestore repository for the <c>payroll_adjustments</c> root collection.
/// REQ-HR-003: Post-finalization compensating records.
/// Append-only — adjustments are never updated or deleted (immutable once created).
/// </summary>
public sealed class PayrollAdjustmentRepository : BaseFirestoreRepository<PayrollAdjustment>
{
    public PayrollAdjustmentRepository(FirestoreDb db, ILogger<PayrollAdjustmentRepository> logger) : base(db, logger) { }

    protected override string CollectionName => "payroll_adjustments";
    protected override ZenoHrErrorCode NotFoundErrorCode => ZenoHrErrorCode.PayrollAdjustmentNotFound;

    // ── Reads ─────────────────────────────────────────────────────────────────

    /// <summary>Gets a specific adjustment by ID, verifying tenant ownership.</summary>
    public Task<Result<PayrollAdjustment>> GetByAdjustmentIdAsync(
        string tenantId, string adjustmentId, CancellationToken ct = default)
        => GetByIdAsync(tenantId, adjustmentId, ct);

    /// <summary>
    /// Lists all adjustments for a specific payroll run, newest first.
    /// REQ-HR-003: HR Manager reviews adjustments per run before SARS filing.
    /// </summary>
    public Task<IReadOnlyList<PayrollAdjustment>> ListByRunAsync(
        string tenantId, string payrollRunId, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId)
            .WhereEqualTo("payroll_run_id", payrollRunId)
            .OrderByDescending("created_at");
        return ExecuteQueryAsync(query, ct);
    }

    /// <summary>
    /// Lists all adjustments affecting a specific employee, across all runs.
    /// Useful for employee audit history and IRP5 reconciliation.
    /// </summary>
    public Task<IReadOnlyList<PayrollAdjustment>> ListByEmployeeAsync(
        string tenantId, string employeeId, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId)
            .WhereEqualTo("employee_id", employeeId)
            .OrderByDescending("created_at");
        return ExecuteQueryAsync(query, ct);
    }

    // ── Write (append-only) ───────────────────────────────────────────────────

    /// <summary>
    /// Creates a new adjustment document. Uses write-once semantics.
    /// CTL-SARS-001: Adjustments are immutable once created.
    /// </summary>
    public Task<Result> AppendAsync(PayrollAdjustment adjustment, CancellationToken ct = default)
        => CreateDocumentAsync(adjustment.AdjustmentId, adjustment, ct);

    // ── Hydration ─────────────────────────────────────────────────────────────

    protected override PayrollAdjustment FromSnapshot(DocumentSnapshot snapshot)
    {
        var adjustmentType = ParseAdjustmentType(snapshot.GetValue<string>("adjustment_type"));

        IReadOnlyList<string> affectedFields = [];
        if (snapshot.TryGetValue<List<object>>("affected_fields", out var rawFields))
            affectedFields = rawFields.Select(o => o?.ToString() ?? "").ToList();

        string? approvedBy = null;
        snapshot.TryGetValue("approved_by", out approvedBy);

        return PayrollAdjustment.Reconstitute(
            adjustmentId: snapshot.Id,
            tenantId: snapshot.GetValue<string>("tenant_id"),
            payrollRunId: snapshot.GetValue<string>("payroll_run_id"),
            employeeId: snapshot.GetValue<string>("employee_id"),
            adjustmentType: adjustmentType,
            reason: snapshot.GetValue<string>("reason"),
            amount: ReadMoney(snapshot, "amount_zar"),
            affectedFields: affectedFields,
            createdBy: snapshot.GetValue<string>("created_by"),
            approvedBy: approvedBy,
            createdAt: snapshot.GetValue<Timestamp>("created_at").ToDateTimeOffset());
    }

    // ── Serialisation ─────────────────────────────────────────────────────────

    protected override Dictionary<string, object?> ToDocument(PayrollAdjustment a) => new()
    {
        ["tenant_id"] = a.TenantId,
        ["adjustment_id"] = a.AdjustmentId,
        ["payroll_run_id"] = a.PayrollRunId,
        ["employee_id"] = a.EmployeeId,
        ["adjustment_type"] = ToAdjustmentTypeString(a.AdjustmentType),
        ["reason"] = a.Reason,
        ["amount_zar"] = a.Amount.ToFirestoreString(),
        ["affected_fields"] = a.AffectedFields.ToList(),
        ["created_by"] = a.CreatedBy,
        ["approved_by"] = a.ApprovedBy,
        ["created_at"] = Timestamp.FromDateTimeOffset(a.CreatedAt),
        ["schema_version"] = a.SchemaVersion,
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MoneyZAR ReadMoney(DocumentSnapshot snapshot, string field)
    {
        if (snapshot.TryGetValue<string>(field, out var str) && !string.IsNullOrWhiteSpace(str))
            return MoneyZAR.FromFirestoreString(str);
        return MoneyZAR.Zero;
    }

    private static string ToAdjustmentTypeString(PayrollAdjustmentType t) => t switch
    {
        PayrollAdjustmentType.Correction => "correction",
        PayrollAdjustmentType.Reversal => "reversal",
        PayrollAdjustmentType.Supplementary => "supplementary",
        _ => "correction",
    };

    private static PayrollAdjustmentType ParseAdjustmentType(string v) => v switch
    {
        "correction" => PayrollAdjustmentType.Correction,
        "reversal" => PayrollAdjustmentType.Reversal,
        "supplementary" => PayrollAdjustmentType.Supplementary,
        _ => PayrollAdjustmentType.Unknown,
    };
}
