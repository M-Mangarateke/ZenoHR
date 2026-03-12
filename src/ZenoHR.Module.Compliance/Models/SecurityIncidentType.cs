// CTL-POPIA-008: Security incident classification for anomaly detection.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Classification of security incidents detected by the anomaly monitoring service.
/// </summary>
public enum SecurityIncidentType
{
    Unknown = 0,
    BruteForceAttempt = 1,
    BulkDataExport = 2,
    OffHoursAccess = 3,
    PrivilegeEscalation = 4,
    UnauthorizedAccess = 5,
    SuspiciousActivity = 6
}
