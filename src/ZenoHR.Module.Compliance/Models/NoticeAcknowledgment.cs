// CTL-POPIA-005: Data Subject Notice Versioning — employee acknowledgment of a specific notice version.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Immutable record of an employee acknowledging a specific version of a data processing notice.
/// Tracks IP address and user agent for audit evidence.
/// </summary>
public sealed record NoticeAcknowledgment
{
    public required string AcknowledgmentId { get; init; }
    public required string TenantId { get; init; }
    public required string EmployeeId { get; init; }
    public required string NoticeId { get; init; }

    /// <summary>The exact version string of the notice that was acknowledged.</summary>
    public required string NoticeVersion { get; init; }

    public required DateTimeOffset AcknowledgedAt { get; init; }

    /// <summary>IP address of the acknowledging client (for audit trail).</summary>
    public string? IpAddress { get; init; }

    /// <summary>User agent of the acknowledging client (for audit trail).</summary>
    public string? UserAgent { get; init; }
}
