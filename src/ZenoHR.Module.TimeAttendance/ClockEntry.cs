// REQ-OPS-003, CTL-BCEA-001: ClockEntry — employee self-service clock-in/clock-out record.
// Firestore schema: docs/schemas/firestore-collections.md §6.3.
// clock_in_at is immutable after creation. Corrections create new entries (source: system_correction).
using ZenoHR.Domain.Errors;

namespace ZenoHR.Module.TimeAttendance;

/// <summary>Source of a clock entry record.</summary>
public enum ClockEntrySource
{
    Unknown = 0,
    EmployeeSelf = 1,
    ManagerEntry = 2,
    SystemCorrection = 3,
}

/// <summary>Lifecycle status of a clock entry.</summary>
public enum ClockEntryStatus
{
    Unknown = 0,
    Open = 1,       // clocked in, not yet out
    Completed = 2,  // clocked out
    Flagged = 3,    // manager-flagged concern
}

/// <summary>
/// Represents a single clock-in/clock-out record for an employee.
/// <para>
/// <b>Immutability</b>: <c>clock_in_at</c> is immutable after creation.
/// Corrections require a new entry with <c>source = SystemCorrection</c>.
/// An employee may have at most one <c>Open</c> entry per day.
/// </para>
/// REQ-OPS-003: Supports self-service clock-in from the <c>14-clock-in.html</c> screen.
/// </summary>
public sealed class ClockEntry
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Firestore document ID. Pattern: <c>ce_&lt;uuid7&gt;</c>. Immutable.</summary>
    public string EntryId { get; }

    /// <summary>Tenant isolation key. Immutable.</summary>
    public string TenantId { get; }

    /// <summary>FK to employees collection.</summary>
    public string EmployeeId { get; }

    // ── Time fields ───────────────────────────────────────────────────────────

    /// <summary>Server-side timestamp when the employee pressed Clock In. Immutable after creation.</summary>
    public DateTimeOffset ClockInAt { get; }

    /// <summary>Server-side timestamp when the employee pressed Clock Out. Null until clocked out.</summary>
    public DateTimeOffset? ClockOutAt { get; private set; }

    /// <summary>
    /// Derived hours: (ClockOutAt - ClockInAt) in decimal hours.
    /// Null until <see cref="ClockOutAt"/> is set. Never accepted from client input.
    /// </summary>
    public decimal? CalculatedHours { get; private set; }

    /// <summary>Calendar date of the clock-in (date-scoped for daily queries).</summary>
    public DateOnly Date { get; }

    // ── Classification ────────────────────────────────────────────────────────

    public ClockEntrySource Source { get; }
    public ClockEntryStatus Status { get; private set; }

    /// <summary>Manager note when status is Flagged.</summary>
    public string? FlagNote { get; private set; }

    /// <summary>FK to timesheets/{ts_id}/time_entries once aggregated. Null until aggregated.</summary>
    public string? LinkedTimeEntryId { get; private set; }

    // ── Audit ─────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string SchemaVersion { get; } = "1.0";

    // ── Constructor ───────────────────────────────────────────────────────────

    private ClockEntry(
        string entryId, string tenantId, string employeeId,
        DateTimeOffset clockInAt, DateOnly date, ClockEntrySource source, DateTimeOffset now)
    {
        EntryId = entryId;
        TenantId = tenantId;
        EmployeeId = employeeId;
        ClockInAt = clockInAt;
        Date = date;
        Source = source;
        Status = ClockEntryStatus.Open;
        CreatedAt = now;
        UpdatedAt = now;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new clock-in entry in <see cref="ClockEntryStatus.Open"/> status.
    /// REQ-OPS-003
    /// </summary>
    public static Result<ClockEntry> ClockIn(
        string entryId, string tenantId, string employeeId,
        ClockEntrySource source, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(entryId))
            return Result<ClockEntry>.Failure(ZenoHrErrorCode.ValidationFailed, "EntryId is required.");
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<ClockEntry>.Failure(ZenoHrErrorCode.ValidationFailed, "TenantId is required.");
        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<ClockEntry>.Failure(ZenoHrErrorCode.ValidationFailed, "EmployeeId is required.");
        if (source == ClockEntrySource.Unknown)
            return Result<ClockEntry>.Failure(ZenoHrErrorCode.ValidationFailed, "Source must not be Unknown.");

        var date = DateOnly.FromDateTime(now.UtcDateTime);
        return Result<ClockEntry>.Success(new ClockEntry(entryId, tenantId, employeeId, now, date, source, now));
    }

    // ── State transitions ─────────────────────────────────────────────────────

    /// <summary>
    /// Records the clock-out timestamp and calculates hours.
    /// Only valid when status is <see cref="ClockEntryStatus.Open"/>.
    /// </summary>
    public Result<ClockEntry> ClockOut(DateTimeOffset clockOutAt, DateTimeOffset now)
    {
        if (Status != ClockEntryStatus.Open)
            return Result<ClockEntry>.Failure(ZenoHrErrorCode.ValidationFailed,
                $"Cannot clock out: entry is in '{Status}' status (expected Open).");
        if (clockOutAt <= ClockInAt)
            return Result<ClockEntry>.Failure(ZenoHrErrorCode.ValueOutOfRange,
                "ClockOutAt must be after ClockInAt.");

        ClockOutAt = clockOutAt;
        CalculatedHours = (decimal)(clockOutAt - ClockInAt).TotalHours;
        Status = ClockEntryStatus.Completed;
        UpdatedAt = now;

        return Result<ClockEntry>.Success(this);
    }

    /// <summary>
    /// Flags the entry with a manager note. Used for suspected absence or anomalies.
    /// </summary>
    public Result<ClockEntry> Flag(string flagNote, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(flagNote))
            return Result<ClockEntry>.Failure(ZenoHrErrorCode.ValidationFailed, "FlagNote is required.");

        FlagNote = flagNote;
        Status = ClockEntryStatus.Flagged;
        UpdatedAt = now;
        return Result<ClockEntry>.Success(this);
    }

    /// <summary>Links this entry to a timesheet time entry after weekly aggregation.</summary>
    public void LinkToTimeEntry(string timeEntryId, DateTimeOffset now)
    {
        LinkedTimeEntryId = timeEntryId;
        UpdatedAt = now;
    }

    // ── Reconstitution ────────────────────────────────────────────────────────

    public static ClockEntry Reconstitute(
        string entryId, string tenantId, string employeeId,
        DateTimeOffset clockInAt, DateTimeOffset? clockOutAt, decimal? calculatedHours,
        DateOnly date, ClockEntrySource source, ClockEntryStatus status,
        string? flagNote, string? linkedTimeEntryId,
        DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        var entry = new ClockEntry(entryId, tenantId, employeeId, clockInAt, date, source, createdAt)
        {
            ClockOutAt = clockOutAt,
            CalculatedHours = calculatedHours,
            Status = status,
            FlagNote = flagNote,
            LinkedTimeEntryId = linkedTimeEntryId,
            UpdatedAt = updatedAt,
        };
        return entry;
    }
}
