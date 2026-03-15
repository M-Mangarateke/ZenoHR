// REQ-HR-002, CTL-BCEA-003, CTL-BCEA-004: Leave balance and accrual ledger repository.
// leave_balances: mutable (upsert). accrual_ledger subcollection: append-only (write-once).
// SaveWithLedgerEntriesAsync writes balance + pending ledger entries atomically.

using Microsoft.Extensions.Logging;
using System.Globalization;
using Google.Cloud.Firestore;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Leave.Aggregates;

namespace ZenoHR.Infrastructure.Firestore;

/// <summary>
/// Repository for <c>leave_balances</c> and the <c>accrual_ledger</c> subcollection.
/// Mutations to a balance always produce pending <see cref="AccrualLedgerEntry"/> records
/// which are flushed atomically after the balance write.
/// CTL-BCEA-003: Balance cannot go negative without policy exception.
/// </summary>
public sealed partial class LeaveBalanceRepository : BaseFirestoreRepository<LeaveBalance>
{
    public LeaveBalanceRepository(FirestoreDb db, ILogger<LeaveBalanceRepository> logger) : base(db, logger) { }

    protected override string CollectionName => "leave_balances";
    protected override ZenoHrErrorCode NotFoundErrorCode => ZenoHrErrorCode.LeaveBalanceNotFound;

    // ── Hydration ────────────────────────────────────────────────────────────

    protected override LeaveBalance FromSnapshot(DocumentSnapshot snapshot)
    {
        var leaveType = ParseLeaveType(snapshot.GetValue<string>("leave_type"));
        var lastAccrualDate = DateOnly.FromDateTime(
            snapshot.GetValue<Timestamp>("last_accrual_date").ToDateTime());

        return LeaveBalance.Reconstitute(
            balanceId: snapshot.Id,
            tenantId: snapshot.GetValue<string>("tenant_id"),
            employeeId: snapshot.GetValue<string>("employee_id"),
            leaveType: leaveType,
            cycleId: snapshot.GetValue<string>("cycle_id"),
            accruedHours: ToDecimal(snapshot, "accrued_hours"),
            consumedHours: ToDecimal(snapshot, "consumed_hours"),
            adjustmentHours: ToDecimal(snapshot, "adjustment_hours"),
            policyVersion: snapshot.GetValue<string>("policy_version"),
            lastAccrualDate: lastAccrualDate,
            createdAt: snapshot.GetValue<Timestamp>("created_at").ToDateTimeOffset(),
            updatedAt: snapshot.GetValue<Timestamp>("updated_at").ToDateTimeOffset());
    }

    // ── Serialisation ────────────────────────────────────────────────────────

    protected override Dictionary<string, object?> ToDocument(LeaveBalance b) => new()
    {
        ["tenant_id"] = b.TenantId,
        ["balance_id"] = b.BalanceId,
        ["employee_id"] = b.EmployeeId,
        ["leave_type"] = ToLeaveTypeString(b.LeaveType),
        ["cycle_id"] = b.CycleId,
        // hours stored as string for decimal precision (monetary-precision convention)
        ["accrued_hours"] = b.AccruedHours.ToString(CultureInfo.InvariantCulture),
        ["consumed_hours"] = b.ConsumedHours.ToString(CultureInfo.InvariantCulture),
        ["adjustment_hours"] = b.AdjustmentHours.ToString(CultureInfo.InvariantCulture),
        ["available_hours"] = b.AvailableHours.ToString(CultureInfo.InvariantCulture),
        ["policy_version"] = b.PolicyVersion,
        ["last_accrual_date"] = Timestamp.FromDateTime(
            b.LastAccrualDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
        ["created_at"] = Timestamp.FromDateTimeOffset(b.CreatedAt),
        ["updated_at"] = Timestamp.FromDateTimeOffset(b.UpdatedAt),
        ["schema_version"] = b.SchemaVersion,
    };

    // ── Public reads ─────────────────────────────────────────────────────────

    /// <summary>Gets a balance by its deterministic ID. REQ-HR-002</summary>
    public Task<Result<LeaveBalance>> GetByBalanceIdAsync(
        string tenantId, string balanceId, CancellationToken ct = default)
        => GetByIdAsync(tenantId, balanceId, ct);

    /// <summary>
    /// Gets the leave balance for a specific employee, leave type, and cycle.
    /// Returns <see cref="ZenoHrErrorCode.LeaveBalanceNotFound"/> if no balance exists yet.
    /// REQ-HR-002
    /// </summary>
    public async Task<Result<LeaveBalance>> GetByEmployeeAndTypeAsync(
        string tenantId, string employeeId, LeaveType leaveType, string cycleId,
        CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId)
            .WhereEqualTo("employee_id", employeeId)
            .WhereEqualTo("leave_type", ToLeaveTypeString(leaveType))
            .WhereEqualTo("cycle_id", cycleId)
            .Limit(1);

        var results = await ExecuteQueryAsync(query, ct);
        return results.Count == 0
            ? Result<LeaveBalance>.Failure(ZenoHrErrorCode.LeaveBalanceNotFound,
                $"No {leaveType} balance for employee '{employeeId}' in cycle '{cycleId}'.")
            : Result<LeaveBalance>.Success(results[0]);
    }

    /// <summary>Lists all leave balances for an employee across all leave types and cycles.</summary>
    public Task<IReadOnlyList<LeaveBalance>> ListByEmployeeAsync(
        string tenantId, string employeeId, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId)
            .WhereEqualTo("employee_id", employeeId)
            .OrderBy("leave_type");
        return ExecuteQueryAsync(query, ct);
    }

    // ── Writes ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves the balance document and appends all pending <see cref="AccrualLedgerEntry"/> records
    /// to the <c>accrual_ledger</c> subcollection atomically using a Firestore transaction.
    /// Prevents race conditions on concurrent leave balance updates.
    /// Pending ledger entries are write-once (append-only). CTL-BCEA-003.
    /// </summary>
    public async Task<Result> SaveWithLedgerEntriesAsync(
        LeaveBalance balance, CancellationToken ct = default)
    {
        var pendingEntries = balance.PopPendingEntries();
        var balanceDoc = ToDocument(balance);
        var balanceRef = Collection.Document(balance.BalanceId);

        try
        {
            await Db.RunTransactionAsync(async transaction =>
            {
                // 1. Upsert the balance document within the transaction
                transaction.Set(balanceRef, balanceDoc);

                // 2. Append pending ledger entries (write-once) within the same transaction
                foreach (var entry in pendingEntries)
                {
                    var entryDoc = BuildLedgerEntryDocument(entry);
                    var ledgerRef = balanceRef
                        .Collection("accrual_ledger")
                        .Document(entry.LedgerEntryId);

                    // Read to check existence within transaction (Create is not available on Transaction)
                    var existingSnap = await transaction.GetSnapshotAsync(ledgerRef, ct);
                    if (existingSnap.Exists)
                    {
                        throw new InvalidOperationException(
                            $"Ledger entry '{entry.LedgerEntryId}' already exists — append-only invariant violated.");
                    }

                    transaction.Set(ledgerRef, entryDoc);
                }
            }, cancellationToken: ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("append-only invariant"))
        {
            return Result.Failure(ZenoHrErrorCode.FirestoreWriteConflict, ex.Message);
        }
        catch (Exception ex)
        {
            LogTransactionFailed(Logger, balance.BalanceId, ex);
            return Result.Failure(ZenoHrErrorCode.FirestoreWriteConflict,
                $"Atomic write failed for balance '{balance.BalanceId}': {ex.Message}");
        }

        return Result.Success();
    }

    /// <summary>Upserts the balance only (no ledger entries). For reconstitution or admin corrections.</summary>
    public Task<Result> SaveAsync(LeaveBalance balance, CancellationToken ct = default)
        => SetDocumentAsync(balance.BalanceId, balance, ct);

    /// <summary>
    /// Gets paginated accrual ledger entries for a balance, oldest-first.
    /// CTL-BCEA-003: Ledger must reconcile to balance totals.
    /// </summary>
    public async Task<IReadOnlyList<AccrualLedgerEntry>> GetLedgerEntriesAsync(
        string balanceId, int limit = 100, CancellationToken ct = default)
    {
        var query = Collection
            .Document(balanceId)
            .Collection("accrual_ledger")
            .OrderBy("created_at")
            .Limit(limit);

        var snapshot = await query.GetSnapshotAsync(ct);
        return snapshot.Documents.Select(FromLedgerSnapshot).ToList();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static AccrualLedgerEntry FromLedgerSnapshot(DocumentSnapshot snapshot)
    {
        var entryType = Enum.TryParse<AccrualEntryType>(
            snapshot.GetValue<string>("entry_type"), ignoreCase: true, out var et)
            ? et : AccrualEntryType.Unknown;

        string? leaveRequestId = null;
        snapshot.TryGetValue("leave_request_id", out leaveRequestId);

        return AccrualLedgerEntry.Reconstitute(
            ledgerEntryId: snapshot.Id,
            balanceId: snapshot.GetValue<string>("balance_id"),
            tenantId: snapshot.GetValue<string>("tenant_id"),
            employeeId: snapshot.GetValue<string>("employee_id"),
            entryType: entryType,
            hours: ToDecimalFromSnapshot(snapshot, "hours"),
            effectiveDate: DateOnly.FromDateTime(snapshot.GetValue<Timestamp>("effective_date").ToDateTime()),
            reasonCode: snapshot.GetValue<string>("reason_code"),
            leaveRequestId: leaveRequestId,
            policyVersion: snapshot.GetValue<string>("policy_version"),
            postedBy: snapshot.GetValue<string>("posted_by"),
            createdAt: snapshot.GetValue<Timestamp>("created_at").ToDateTimeOffset());
    }

    private static Dictionary<string, object?> BuildLedgerEntryDocument(AccrualLedgerEntry entry) => new()
    {
        ["ledger_entry_id"] = entry.LedgerEntryId,
        ["balance_id"] = entry.BalanceId,
        ["tenant_id"] = entry.TenantId,
        ["employee_id"] = entry.EmployeeId,
        ["entry_type"] = entry.EntryType.ToString().ToLowerInvariant(),
        ["hours"] = entry.Hours.ToString(CultureInfo.InvariantCulture),
        ["effective_date"] = Timestamp.FromDateTime(
            entry.EffectiveDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
        ["reason_code"] = entry.ReasonCode,
        ["leave_request_id"] = entry.LeaveRequestId,
        ["policy_version"] = entry.PolicyVersion,
        ["posted_by"] = entry.PostedBy,
        ["created_at"] = Timestamp.FromDateTimeOffset(entry.CreatedAt),
    };

    // Prefer string (precision-safe); fall back to double/long for legacy data
    private static decimal ToDecimal(DocumentSnapshot snapshot, string field)
    {
        // Prefer string (precision-safe); fall back to double/long for legacy data.
        // Guard: TryGetValue<double/long> throws ArgumentException when field is null in Firestore.
        if (snapshot.TryGetValue<string>(field, out var s) && decimal.TryParse(s, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        if (!snapshot.ContainsField(field) || snapshot.GetValue<object>(field) == null) return 0m;
        if (snapshot.TryGetValue<double>(field, out var d)) return (decimal)d;
        if (snapshot.TryGetValue<long>(field, out var l)) return l;
        return 0m;
    }

    private static decimal ToDecimalFromSnapshot(DocumentSnapshot snapshot, string field)
        => ToDecimal(snapshot, field);

    private static string ToLeaveTypeString(LeaveType leaveType) => leaveType switch
    {
        LeaveType.Annual => "annual",
        LeaveType.Sick => "sick",
        LeaveType.FamilyResponsibility => "family_responsibility",
        LeaveType.Maternity => "maternity",
        LeaveType.Parental => "parental",
        _ => leaveType.ToString().ToLowerInvariant(),
    };

    private static LeaveType ParseLeaveType(string value) => value switch
    {
        "annual" => LeaveType.Annual,
        "sick" => LeaveType.Sick,
        "family_responsibility" => LeaveType.FamilyResponsibility,
        "maternity" => LeaveType.Maternity,
        "parental" => LeaveType.Parental,
        _ => LeaveType.Unknown,
    };

    // ── Diagnostic logging ────────────────────────────────────────────────────

    [LoggerMessage(EventId = 2010, Level = LogLevel.Error,
        Message = "Firestore transaction failed for leave balance {BalanceId}")]
    private static partial void LogTransactionFailed(ILogger logger, string balanceId, Exception ex);
}
