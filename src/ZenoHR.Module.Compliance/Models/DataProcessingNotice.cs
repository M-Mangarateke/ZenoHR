// CTL-POPIA-005: Data Subject Notice Versioning — POPIA §18 processing notification record.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Versioned data processing notice that must be acknowledged by all data subjects.
/// POPIA §18 requires notification of processing purpose, categories, recipients, and rights.
/// Each version change requires re-acknowledgment from all employees.
/// </summary>
public sealed record DataProcessingNotice
{
    public required string NoticeId { get; init; }
    public required string TenantId { get; init; }

    /// <summary>Semantic version string (e.g., "1.0.0", "2.1.0").</summary>
    public required string Version { get; init; }

    public required string Title { get; init; }

    /// <summary>Full notice content (processing purpose, categories, recipients, rights).</summary>
    public required string Content { get; init; }

    public required DateTimeOffset EffectiveFrom { get; init; }
    public required string CreatedBy { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Only one version of a notice should be active at a time.</summary>
    public required bool IsActive { get; init; }
}
