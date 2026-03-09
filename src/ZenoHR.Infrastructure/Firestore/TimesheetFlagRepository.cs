// REQ-OPS-003: TimesheetFlag Firestore repository — manager attendance verification workflow.
// Collection: timesheet_flags (root, tenant-scoped).

using Microsoft.Extensions.Logging;
using Google.Cloud.Firestore;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.TimeAttendance;

namespace ZenoHR.Infrastructure.Firestore;

/// <summary>
/// Firestore repository for the <c>timesheet_flags</c> root collection.
/// Managers create flags for suspected absences or anomalous hours.
/// REQ-OPS-003.
/// </summary>
public sealed class TimesheetFlagRepository : BaseFirestoreRepository<TimesheetFlag>
{
    public TimesheetFlagRepository(FirestoreDb db, ILogger<TimesheetFlagRepository> logger) : base(db, logger) { }

    protected override string CollectionName => "timesheet_flags";
    protected override ZenoHrErrorCode NotFoundErrorCode => ZenoHrErrorCode.ValidationFailed;

    // ── Hydration ────────────────────────────────────────────────────────────

    protected override TimesheetFlag FromSnapshot(DocumentSnapshot snapshot)
    {
        var reason = ParseReason(snapshot.GetValue<string>("reason"));
        var status = ParseStatus(snapshot.GetValue<string>("status"));

        string? notes = null;
        snapshot.TryGetValue("notes", out notes);

        string? resolvedBy = null;
        snapshot.TryGetValue("resolved_by", out resolvedBy);

        DateTimeOffset? resolvedAt = null;
        if (snapshot.TryGetValue<Timestamp>("resolved_at", out var rat))
            resolvedAt = rat.ToDateTimeOffset();

        return TimesheetFlag.Reconstitute(
            flagId: snapshot.Id,
            tenantId: snapshot.GetValue<string>("tenant_id"),
            employeeId: snapshot.GetValue<string>("employee_id"),
            flaggedBy: snapshot.GetValue<string>("flagged_by"),
            flagDate: DateOnly.FromDateTime(snapshot.GetValue<Timestamp>("flag_date").ToDateTime()),
            reason: reason,
            notes: notes,
            status: status,
            resolvedBy: resolvedBy,
            resolvedAt: resolvedAt,
            createdAt: snapshot.GetValue<Timestamp>("created_at").ToDateTimeOffset(),
            updatedAt: snapshot.GetValue<Timestamp>("updated_at").ToDateTimeOffset());
    }

    // ── Serialisation ────────────────────────────────────────────────────────

    protected override Dictionary<string, object?> ToDocument(TimesheetFlag f) => new()
    {
        ["tenant_id"] = f.TenantId,
        ["flag_id"] = f.FlagId,
        ["employee_id"] = f.EmployeeId,
        ["flagged_by"] = f.FlaggedBy,
        ["flag_date"] = Timestamp.FromDateTime(f.FlagDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
        ["reason"] = ToReasonString(f.Reason),
        ["notes"] = f.Notes,
        ["status"] = ToStatusString(f.Status),
        ["resolved_by"] = f.ResolvedBy,
        ["resolved_at"] = f.ResolvedAt.HasValue ? Timestamp.FromDateTimeOffset(f.ResolvedAt.Value) : (object?)null,
        ["created_at"] = Timestamp.FromDateTimeOffset(f.CreatedAt),
        ["updated_at"] = Timestamp.FromDateTimeOffset(f.UpdatedAt),
    };

    // ── Public reads ─────────────────────────────────────────────────────────

    /// <summary>Gets all open timesheet flags for a set of employees (manager's team view).</summary>
    public Task<IReadOnlyList<TimesheetFlag>> ListOpenForEmployeesAsync(
        string tenantId, IReadOnlyList<string> employeeIds, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId)
            .WhereIn("employee_id", employeeIds)
            .WhereEqualTo("status", "open")
            .OrderByDescending("flag_date");
        return ExecuteQueryAsync(query, ct);
    }

    /// <summary>Lists all flags for a specific employee, newest-first.</summary>
    public Task<IReadOnlyList<TimesheetFlag>> ListByEmployeeAsync(
        string tenantId, string employeeId, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId)
            .WhereEqualTo("employee_id", employeeId)
            .OrderByDescending("flag_date");
        return ExecuteQueryAsync(query, ct);
    }

    // ── Writes ───────────────────────────────────────────────────────────────

    /// <summary>Upserts a timesheet flag.</summary>
    public Task<Result> SaveAsync(TimesheetFlag flag, CancellationToken ct = default)
        => SetDocumentAsync(flag.FlagId, flag, ct);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TimesheetFlagReason ParseReason(string value) => value switch
    {
        "suspected_absence" => TimesheetFlagReason.SuspectedAbsence,
        "suspicious_hours" => TimesheetFlagReason.SuspiciousHours,
        "missing_clock_out" => TimesheetFlagReason.MissingClockOut,
        "other" => TimesheetFlagReason.Other,
        _ => TimesheetFlagReason.Unknown,
    };

    private static string ToReasonString(TimesheetFlagReason reason) => reason switch
    {
        TimesheetFlagReason.SuspectedAbsence => "suspected_absence",
        TimesheetFlagReason.SuspiciousHours => "suspicious_hours",
        TimesheetFlagReason.MissingClockOut => "missing_clock_out",
        TimesheetFlagReason.Other => "other",
        _ => "other",
    };

    private static TimesheetFlagStatus ParseStatus(string value) => value switch
    {
        "open" => TimesheetFlagStatus.Open,
        "resolved" => TimesheetFlagStatus.Resolved,
        "dismissed" => TimesheetFlagStatus.Dismissed,
        _ => TimesheetFlagStatus.Unknown,
    };

    private static string ToStatusString(TimesheetFlagStatus status) => status switch
    {
        TimesheetFlagStatus.Open => "open",
        TimesheetFlagStatus.Resolved => "resolved",
        TimesheetFlagStatus.Dismissed => "dismissed",
        _ => "open",
    };
}
