// REQ-OPS-003: ClockEntry Firestore repository — employee self-service clock-in/clock-out.
// Collection: clock_entries (root).
// clock_in_at is immutable — corrections create new entries (source: system_correction).

using Microsoft.Extensions.Logging;
using System.Globalization;
using Google.Cloud.Firestore;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.TimeAttendance;

namespace ZenoHR.Infrastructure.Firestore;

/// <summary>
/// Firestore repository for the <c>clock_entries</c> root collection.
/// Supports employee self-service clock-in/clock-out and manager team status queries.
/// REQ-OPS-003: At most one Open entry per employee per day (enforced at application layer).
/// </summary>
public sealed class ClockEntryRepository : BaseFirestoreRepository<ClockEntry>
{
    public ClockEntryRepository(FirestoreDb db, ILogger<ClockEntryRepository> logger) : base(db, logger) { }

    protected override string CollectionName => "clock_entries";
    protected override ZenoHrErrorCode NotFoundErrorCode => ZenoHrErrorCode.ValidationFailed; // no dedicated code yet

    // ── Hydration ────────────────────────────────────────────────────────────

    protected override ClockEntry FromSnapshot(DocumentSnapshot snapshot)
    {
        var source = ParseSource(snapshot.GetValue<string>("source"));
        var status = ParseStatus(snapshot.GetValue<string>("status"));

        DateTimeOffset? clockOutAt = null;
        if (snapshot.ContainsField("clock_out_at") && snapshot.GetValue<object>("clock_out_at") != null)
            clockOutAt = snapshot.GetValue<Timestamp>("clock_out_at").ToDateTimeOffset();

        decimal? calculatedHours = null;
        if (snapshot.TryGetValue<string>("calculated_hours", out var chStr)
            && decimal.TryParse(chStr, CultureInfo.InvariantCulture, out var chParsed))
            calculatedHours = chParsed;
        else if (snapshot.TryGetValue<double>("calculated_hours", out var ch))
            calculatedHours = (decimal)ch;

        string? flagNote = null;
        snapshot.TryGetValue("flag_note", out flagNote);

        string? linkedTimeEntryId = null;
        snapshot.TryGetValue("linked_time_entry_id", out linkedTimeEntryId);

        return ClockEntry.Reconstitute(
            entryId: snapshot.Id,
            tenantId: snapshot.GetValue<string>("tenant_id"),
            employeeId: snapshot.GetValue<string>("employee_id"),
            clockInAt: snapshot.GetValue<Timestamp>("clock_in_at").ToDateTimeOffset(),
            clockOutAt: clockOutAt,
            calculatedHours: calculatedHours,
            date: DateOnly.FromDateTime(snapshot.GetValue<Timestamp>("date").ToDateTime()),
            source: source,
            status: status,
            flagNote: flagNote,
            linkedTimeEntryId: linkedTimeEntryId,
            createdAt: snapshot.GetValue<Timestamp>("created_at").ToDateTimeOffset(),
            updatedAt: snapshot.GetValue<Timestamp>("updated_at").ToDateTimeOffset());
    }

    // ── Serialisation ────────────────────────────────────────────────────────

    protected override Dictionary<string, object?> ToDocument(ClockEntry entry) => new()
    {
        ["tenant_id"] = entry.TenantId,
        ["entry_id"] = entry.EntryId,
        ["employee_id"] = entry.EmployeeId,
        ["clock_in_at"] = Timestamp.FromDateTimeOffset(entry.ClockInAt),
        ["clock_out_at"] = entry.ClockOutAt.HasValue
            ? Timestamp.FromDateTimeOffset(entry.ClockOutAt.Value)
            : (object?)null,
        ["calculated_hours"] = entry.CalculatedHours.HasValue ? entry.CalculatedHours.Value.ToString(CultureInfo.InvariantCulture) : (object?)null,
        ["date"] = Timestamp.FromDateTime(entry.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
        ["source"] = ToSourceString(entry.Source),
        ["status"] = ToStatusString(entry.Status),
        ["flag_note"] = entry.FlagNote,
        ["linked_time_entry_id"] = entry.LinkedTimeEntryId,
        ["created_at"] = Timestamp.FromDateTimeOffset(entry.CreatedAt),
        ["updated_at"] = Timestamp.FromDateTimeOffset(entry.UpdatedAt),
        ["schema_version"] = entry.SchemaVersion,
    };

    // ── Public reads ─────────────────────────────────────────────────────────

    /// <summary>Gets a clock entry by ID, verifying tenant ownership.</summary>
    public Task<Result<ClockEntry>> GetByEntryIdAsync(
        string tenantId, string entryId, CancellationToken ct = default)
        => GetByIdAsync(tenantId, entryId, ct);

    /// <summary>
    /// Gets the current open clock entry for an employee on the given date, if any.
    /// Used to prevent double clock-in. REQ-OPS-003.
    /// </summary>
    public async Task<ClockEntry?> GetOpenEntryAsync(
        string tenantId, string employeeId, DateOnly date, CancellationToken ct = default)
    {
        var dateTs = Timestamp.FromDateTime(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var query = TenantQuery(tenantId)
            .WhereEqualTo("employee_id", employeeId)
            .WhereEqualTo("date", dateTs)
            .WhereEqualTo("status", "open")
            .Limit(1);

        var results = await ExecuteQueryAsync(query, ct);
        return results.Count == 0 ? null : results[0];
    }

    /// <summary>
    /// Lists clock entries for an employee within a date range.
    /// Used for weekly timesheet aggregation.
    /// </summary>
    public Task<IReadOnlyList<ClockEntry>> ListByEmployeeAndDateRangeAsync(
        string tenantId, string employeeId, DateOnly from, DateOnly to,
        CancellationToken ct = default)
    {
        var fromTs = Timestamp.FromDateTime(from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var toTs = Timestamp.FromDateTime(to.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var query = TenantQuery(tenantId)
            .WhereEqualTo("employee_id", employeeId)
            .WhereGreaterThanOrEqualTo("date", fromTs)
            .WhereLessThanOrEqualTo("date", toTs)
            .OrderBy("clock_in_at");
        return ExecuteQueryAsync(query, ct);
    }

    /// <summary>
    /// Lists all open (clocked-in) entries for a set of employees on today's date.
    /// Used by the manager team status panel on the Clock-In screen.
    /// </summary>
    public Task<IReadOnlyList<ClockEntry>> ListOpenForEmployeesAsync(
        string tenantId, IReadOnlyList<string> employeeIds, DateOnly date,
        CancellationToken ct = default)
    {
        var dateTs = Timestamp.FromDateTime(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var query = TenantQuery(tenantId)
            .WhereIn("employee_id", employeeIds)
            .WhereEqualTo("date", dateTs)
            .WhereEqualTo("status", "open");
        return ExecuteQueryAsync(query, ct);
    }

    // ── Writes ───────────────────────────────────────────────────────────────

    /// <summary>Upserts a clock entry (create on clock-in, update on clock-out or flag).</summary>
    public Task<Result> SaveAsync(ClockEntry entry, CancellationToken ct = default)
        => SetDocumentAsync(entry.EntryId, entry, ct);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ClockEntrySource ParseSource(string value) => value switch
    {
        "employee_self" => ClockEntrySource.EmployeeSelf,
        "manager_entry" => ClockEntrySource.ManagerEntry,
        "system_correction" => ClockEntrySource.SystemCorrection,
        _ => ClockEntrySource.Unknown,
    };

    private static string ToSourceString(ClockEntrySource source) => source switch
    {
        ClockEntrySource.EmployeeSelf => "employee_self",
        ClockEntrySource.ManagerEntry => "manager_entry",
        ClockEntrySource.SystemCorrection => "system_correction",
        _ => "employee_self",
    };

    private static ClockEntryStatus ParseStatus(string value) => value switch
    {
        "open" => ClockEntryStatus.Open,
        "completed" => ClockEntryStatus.Completed,
        "flagged" => ClockEntryStatus.Flagged,
        _ => ClockEntryStatus.Unknown,
    };

    private static string ToStatusString(ClockEntryStatus status) => status switch
    {
        ClockEntryStatus.Open => "open",
        ClockEntryStatus.Completed => "completed",
        ClockEntryStatus.Flagged => "flagged",
        _ => "open",
    };
}
