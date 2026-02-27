// REQ-COMP-005: All auditable actions must be recorded via AuditEventWriter.
// CTL-POPIA-012: Audit log must capture actor, action, resource, and timestamp.

using ZenoHR.Module.Audit.Domain;

namespace ZenoHR.Infrastructure.Audit;

/// <summary>
/// Request object passed to <see cref="AuditEventWriter.WriteAsync"/> to record an auditable action.
/// </summary>
/// <remarks>
/// PII values must NEVER be stored in <see cref="Metadata"/> — use field names only, not field values.
/// </remarks>
public sealed record WriteAuditEventRequest
{
    /// <summary>Tenant that owns this audit event. Required.</summary>
    public required string TenantId { get; init; }

    /// <summary>Firebase UID of the authenticated actor who performed the action.</summary>
    public required string ActorId { get; init; }

    /// <summary>System role of the actor at the time of the action (e.g., "HRManager").</summary>
    public required string ActorRole { get; init; }

    /// <summary>The type of action performed.</summary>
    public required AuditAction Action { get; init; }

    /// <summary>The type of resource affected.</summary>
    public required AuditResourceType ResourceType { get; init; }

    /// <summary>The Firestore document ID of the affected resource.</summary>
    public required string ResourceId { get; init; }

    /// <summary>
    /// Optional JSON metadata (e.g., list of changed field names).
    /// Must NOT contain PII values — field names only.
    /// </summary>
    public string? Metadata { get; init; }

    /// <summary>
    /// When the action occurred (UTC). Defaults to <see cref="DateTimeOffset.UtcNow"/>.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
