// REQ-OPS-003: TimesheetFlag — manager-created flag for suspected absence or anomalous hours.
// Firestore schema: docs/schemas/firestore-collections.md §6.4.
// Drives the manager verification workflow in the Clock-In screen (14-clock-in.html).
using ZenoHR.Domain.Errors;

namespace ZenoHR.Module.TimeAttendance;

/// <summary>Reason for the timesheet flag.</summary>
public enum TimesheetFlagReason
{
    Unknown = 0,
    SuspectedAbsence = 1,
    SuspiciousHours = 2,
    MissingClockOut = 3,
    Other = 4,
}

/// <summary>Lifecycle status of a timesheet flag.</summary>
public enum TimesheetFlagStatus
{
    Unknown = 0,
    Open = 1,
    Resolved = 2,
    Dismissed = 3,
}

/// <summary>
/// Manager-created flag indicating a suspected attendance issue for an employee on a specific date.
/// Used for the manager verification workflow. REQ-OPS-003.
/// </summary>
public sealed class TimesheetFlag
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Firestore document ID. Pattern: <c>tf_&lt;uuid7&gt;</c>.</summary>
    public string FlagId { get; }

    /// <summary>Tenant isolation key. Immutable.</summary>
    public string TenantId { get; }

    /// <summary>FK to employees collection (flagged employee).</summary>
    public string EmployeeId { get; }

    /// <summary>Actor ID of the Manager who created the flag.</summary>
    public string FlaggedBy { get; }

    // ── Flag details ──────────────────────────────────────────────────────────

    public DateOnly FlagDate { get; }
    public TimesheetFlagReason Reason { get; }
    public string? Notes { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public TimesheetFlagStatus Status { get; private set; }
    public string? ResolvedBy { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    // ── Audit ─────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // ── Constructor ───────────────────────────────────────────────────────────

    private TimesheetFlag(
        string flagId, string tenantId, string employeeId, string flaggedBy,
        DateOnly flagDate, TimesheetFlagReason reason, string? notes, DateTimeOffset now)
    {
        FlagId = flagId;
        TenantId = tenantId;
        EmployeeId = employeeId;
        FlaggedBy = flaggedBy;
        FlagDate = flagDate;
        Reason = reason;
        Notes = notes;
        Status = TimesheetFlagStatus.Open;
        CreatedAt = now;
        UpdatedAt = now;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>Creates a new open timesheet flag. REQ-OPS-003</summary>
    public static Result<TimesheetFlag> Create(
        string flagId, string tenantId, string employeeId, string flaggedBy,
        DateOnly flagDate, TimesheetFlagReason reason, string? notes, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(flagId))
            return Result<TimesheetFlag>.Failure(ZenoHrErrorCode.ValidationFailed, "FlagId is required.");
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<TimesheetFlag>.Failure(ZenoHrErrorCode.ValidationFailed, "TenantId is required.");
        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<TimesheetFlag>.Failure(ZenoHrErrorCode.ValidationFailed, "EmployeeId is required.");
        if (string.IsNullOrWhiteSpace(flaggedBy))
            return Result<TimesheetFlag>.Failure(ZenoHrErrorCode.ValidationFailed, "FlaggedBy is required.");
        if (reason == TimesheetFlagReason.Unknown)
            return Result<TimesheetFlag>.Failure(ZenoHrErrorCode.ValidationFailed, "Reason must not be Unknown.");

        return Result<TimesheetFlag>.Success(
            new TimesheetFlag(flagId, tenantId, employeeId, flaggedBy, flagDate, reason, notes, now));
    }

    // ── State transitions ─────────────────────────────────────────────────────

    /// <summary>Marks the flag as resolved after manager review.</summary>
    public Result<TimesheetFlag> Resolve(string resolvedBy, DateTimeOffset now)
    {
        if (Status != TimesheetFlagStatus.Open)
            return Result<TimesheetFlag>.Failure(ZenoHrErrorCode.ValidationFailed,
                $"Cannot resolve: flag is in '{Status}' status.");

        ResolvedBy = resolvedBy;
        ResolvedAt = now;
        Status = TimesheetFlagStatus.Resolved;
        UpdatedAt = now;
        return Result<TimesheetFlag>.Success(this);
    }

    /// <summary>Dismisses the flag as not requiring action.</summary>
    public Result<TimesheetFlag> Dismiss(string resolvedBy, DateTimeOffset now)
    {
        if (Status != TimesheetFlagStatus.Open)
            return Result<TimesheetFlag>.Failure(ZenoHrErrorCode.ValidationFailed,
                $"Cannot dismiss: flag is in '{Status}' status.");

        ResolvedBy = resolvedBy;
        ResolvedAt = now;
        Status = TimesheetFlagStatus.Dismissed;
        UpdatedAt = now;
        return Result<TimesheetFlag>.Success(this);
    }

    // ── Reconstitution ────────────────────────────────────────────────────────

    public static TimesheetFlag Reconstitute(
        string flagId, string tenantId, string employeeId, string flaggedBy,
        DateOnly flagDate, TimesheetFlagReason reason, string? notes,
        TimesheetFlagStatus status, string? resolvedBy, DateTimeOffset? resolvedAt,
        DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        var flag = new TimesheetFlag(flagId, tenantId, employeeId, flaggedBy, flagDate, reason, notes, createdAt)
        {
            Status = status,
            ResolvedBy = resolvedBy,
            ResolvedAt = resolvedAt,
            UpdatedAt = updatedAt,
        };
        return flag;
    }
}
