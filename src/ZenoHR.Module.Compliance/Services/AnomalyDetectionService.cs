// CTL-POPIA-008: Breach detection and anomaly monitoring service.
// Alert thresholds per PRD-05 §7:
//   - 5 failed auths in 10 min = SEV-2 (BruteForceAttempt)
//   - Bulk export > 50 records in 5 min = SEV-2 (BulkDataExport)
//   - Off-hours privileged access outside 7AM-6PM SAST = SEV-3 (OffHoursAccess)

using System.Globalization;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Models;

namespace ZenoHR.Module.Compliance.Services;

/// <summary>
/// Simple input record for audit log entries consumed by the anomaly detector.
/// </summary>
public sealed record AuditEntry(
    string EventId,
    string Action,
    string UserId,
    DateTimeOffset Timestamp,
    bool IsSuccess);

/// <summary>
/// Detects security anomalies from audit event streams and produces
/// <see cref="SecurityIncident"/> records when thresholds are exceeded.
/// Stateless — all state comes from the caller-supplied event lists.
/// </summary>
public sealed class AnomalyDetectionService
{
    // CTL-POPIA-008: Valid forward-only status transitions for incident lifecycle.
    private static readonly Dictionary<IncidentStatus, IncidentStatus[]> ValidTransitions =
        new Dictionary<IncidentStatus, IncidentStatus[]>
        {
            [IncidentStatus.Detected] = [IncidentStatus.Investigating, IncidentStatus.FalsePositive],
            [IncidentStatus.Investigating] = [IncidentStatus.Contained, IncidentStatus.FalsePositive],
            [IncidentStatus.Contained] = [IncidentStatus.Resolved],
            [IncidentStatus.Resolved] = [],
            [IncidentStatus.FalsePositive] = [],
        };

    /// <summary>
    /// Detects brute-force login attempts: <paramref name="threshold"/> or more failed
    /// authentication events within <paramref name="window"/>.
    /// PRD-05 §7 default: 5 failures in 10 minutes = SEV-2.
    /// </summary>
    // CTL-POPIA-008
    public static Result<SecurityIncident> DetectBruteForce(
        IReadOnlyList<AuditEntry> recentEvents,
        TimeSpan window,
        int threshold)
    {
        ArgumentNullException.ThrowIfNull(recentEvents);

        if (recentEvents.Count == 0)
        {
            return Result<SecurityIncident>.Failure(
                ZenoHrErrorCode.NoAnomalyDetected,
                "No events provided for brute-force detection.");
        }

        var cutoff = recentEvents[^1].Timestamp - window;
        var failedAuths = recentEvents
            .Where(e => !e.IsSuccess && e.Timestamp >= cutoff)
            .ToList();

        if (failedAuths.Count < threshold)
        {
            return Result<SecurityIncident>.Failure(
                ZenoHrErrorCode.NoAnomalyDetected,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Only {0} failed auth(s) in window — threshold is {1}.",
                    failedAuths.Count,
                    threshold));
        }

        var affectedUser = failedAuths
            .GroupBy(e => e.UserId)
            .OrderByDescending(g => g.Count())
            .First()
            .Key;

        var incident = new SecurityIncident
        {
            IncidentId = GenerateIncidentId(),
            TenantId = string.Empty, // Caller must set tenant context
            DetectedAt = DateTimeOffset.UtcNow,
            Severity = BreachSeverity.Medium, // SEV-2 per PRD-05 §7
            IncidentType = SecurityIncidentType.BruteForceAttempt,
            Description = string.Format(
                CultureInfo.InvariantCulture,
                "{0} failed authentication attempts detected in {1} minutes for user {2}.",
                failedAuths.Count,
                window.TotalMinutes,
                affectedUser),
            AffectedUserId = affectedUser,
            Status = IncidentStatus.Detected,
        };

        return Result<SecurityIncident>.Success(incident);
    }

    /// <summary>
    /// Detects unusual bulk data export: more than <paramref name="threshold"/> export
    /// events within a 5-minute sliding window.
    /// PRD-05 §7: bulk export > 50 records = SEV-2.
    /// </summary>
    // CTL-POPIA-008
    public static Result<SecurityIncident> DetectBulkExport(
        IReadOnlyList<AuditEntry> recentEvents,
        int threshold)
    {
        ArgumentNullException.ThrowIfNull(recentEvents);

        if (recentEvents.Count == 0)
        {
            return Result<SecurityIncident>.Failure(
                ZenoHrErrorCode.NoAnomalyDetected,
                "No events provided for bulk export detection.");
        }

        var exportWindow = TimeSpan.FromMinutes(5);
        var cutoff = recentEvents[^1].Timestamp - exportWindow;

        var exportEvents = recentEvents
            .Where(e => e.IsSuccess && e.Timestamp >= cutoff)
            .ToList();

        if (exportEvents.Count <= threshold)
        {
            return Result<SecurityIncident>.Failure(
                ZenoHrErrorCode.NoAnomalyDetected,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Only {0} export event(s) in 5-minute window — threshold is {1}.",
                    exportEvents.Count,
                    threshold));
        }

        var affectedUser = exportEvents
            .GroupBy(e => e.UserId)
            .OrderByDescending(g => g.Count())
            .First()
            .Key;

        var incident = new SecurityIncident
        {
            IncidentId = GenerateIncidentId(),
            TenantId = string.Empty,
            DetectedAt = DateTimeOffset.UtcNow,
            Severity = BreachSeverity.Medium, // SEV-2 per PRD-05 §7
            IncidentType = SecurityIncidentType.BulkDataExport,
            Description = string.Format(
                CultureInfo.InvariantCulture,
                "{0} data export events detected in 5-minute window by user {1} (threshold: {2}).",
                exportEvents.Count,
                affectedUser,
                threshold),
            AffectedUserId = affectedUser,
            Status = IncidentStatus.Detected,
        };

        return Result<SecurityIncident>.Success(incident);
    }

    /// <summary>
    /// Detects privileged actions performed outside business hours.
    /// PRD-05 §7: off-hours privileged access = SEV-3.
    /// </summary>
    // CTL-POPIA-008
    public static Result<SecurityIncident> DetectOffHoursAccess(
        string action,
        DateTimeOffset timestamp,
        TimeSpan businessStart,
        TimeSpan businessEnd)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        var timeOfDay = timestamp.TimeOfDay;

        if (timeOfDay >= businessStart && timeOfDay < businessEnd)
        {
            return Result<SecurityIncident>.Failure(
                ZenoHrErrorCode.NoAnomalyDetected,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Action '{0}' at {1} is within business hours ({2}-{3}).",
                    action,
                    timeOfDay,
                    businessStart,
                    businessEnd));
        }

        var incident = new SecurityIncident
        {
            IncidentId = GenerateIncidentId(),
            TenantId = string.Empty,
            DetectedAt = DateTimeOffset.UtcNow,
            Severity = BreachSeverity.Low, // SEV-3 per PRD-05 §7
            IncidentType = SecurityIncidentType.OffHoursAccess,
            Description = string.Format(
                CultureInfo.InvariantCulture,
                "Privileged action '{0}' performed at {1} outside business hours ({2}-{3}).",
                action,
                timestamp.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
                businessStart,
                businessEnd),
            Status = IncidentStatus.Detected,
        };

        return Result<SecurityIncident>.Success(incident);
    }

    /// <summary>
    /// Transitions a security incident to a new status.
    /// Enforces forward-only state machine — backward transitions are rejected.
    /// </summary>
    // CTL-POPIA-008
    public static Result<SecurityIncident> UpdateStatus(
        SecurityIncident existing,
        IncidentStatus newStatus,
        string resolvedBy,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedBy);

        if (!ValidTransitions.TryGetValue(existing.Status, out var allowed) ||
            !allowed.Contains(newStatus))
        {
            return Result<SecurityIncident>.Failure(
                ZenoHrErrorCode.InvalidIncidentStatusTransition,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Cannot transition incident from {0} to {1}.",
                    existing.Status,
                    newStatus));
        }

        var updated = existing with
        {
            Status = newStatus,
            ResolvedBy = resolvedBy,
            ResolvedAt = newStatus is IncidentStatus.Resolved or IncidentStatus.FalsePositive
                ? timestamp
                : existing.ResolvedAt,
            Notes = newStatus == IncidentStatus.FalsePositive
                ? $"Marked as false positive by {resolvedBy} at {timestamp.ToString("o", CultureInfo.InvariantCulture)}"
                : existing.Notes,
        };

        return Result<SecurityIncident>.Success(updated);
    }

    private static string GenerateIncidentId() =>
        string.Format(
            CultureInfo.InvariantCulture,
            "INC-{0:yyyyMMdd}-{1}",
            DateTime.UtcNow,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8]);
}
