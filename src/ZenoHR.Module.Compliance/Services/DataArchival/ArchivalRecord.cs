// CTL-POPIA-009, CTL-POPIA-015: Individual archival action record.
// Captures the details of a single employee record archival event.

namespace ZenoHR.Module.Compliance.Services.DataArchival;

/// <summary>
/// Represents a single employee record archival action, including the employee identity,
/// retention dates, and the archival metadata. Used as an entry in <see cref="ArchivalReport"/>.
/// </summary>
/// <param name="EmployeeId">The unique identifier of the archived employee.</param>
/// <param name="TenantId">The tenant to which the employee belongs.</param>
/// <param name="TerminationDate">The employee's termination date.</param>
/// <param name="RetentionExpiryDate">The date the retention period expired.</param>
/// <param name="ArchivedAt">The UTC timestamp when the archival was performed.</param>
/// <param name="ArchivedBy">The system identity that performed the archival (typically the background service).</param>
/// <param name="Reason">The reason for archival (e.g., "Retention period elapsed — 5 years post-termination").</param>
public sealed record ArchivalRecord(
    string EmployeeId,
    string TenantId,
    DateOnly TerminationDate,
    DateOnly RetentionExpiryDate,
    DateTimeOffset ArchivedAt,
    string ArchivedBy,
    string Reason);
