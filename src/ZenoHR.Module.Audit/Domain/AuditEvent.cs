// REQ-OPS-003: AuditEvent — immutable, hash-chained audit record.
// CTL-POPIA-006: All PII access must be logged as an AuditEvent.
// Every AuditEvent is write-once (never updated or deleted in Firestore).
// Each event's hash includes the prior event's hash — forming a tamper-evident chain.
// Breaking the chain is a Sev-1 defect.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ZenoHR.Module.Audit.Domain;

/// <summary>
/// Immutable audit record representing a single action taken by an actor on a resource.
/// <para>
/// Each event is linked to the previous event via <see cref="PreviousEventHash"/>, forming
/// a SHA-256 hash chain. Tampering with any stored event breaks the chain and is immediately detectable.
/// </para>
/// <para>
/// <b>Write-once rule</b>: Once persisted to Firestore, an <see cref="AuditEvent"/> must never
/// be modified or deleted. Corrections are recorded as new events referencing the original event ID.
/// </para>
/// </summary>
public sealed class AuditEvent
{
    // Fixed JSON serialization options — field order is CANONICAL and must never change.
    // Changing field order would invalidate all previously computed hashes.
    private static readonly JsonSerializerOptions _canonicalJsonOptions = new()
    {
        WriteIndented = false,
    };

    private AuditEvent(
        string eventId,
        string tenantId,
        string actorId,
        string actorRole,
        AuditAction action,
        AuditResourceType resourceType,
        string resourceId,
        string? metadata,
        DateTimeOffset occurredAt,
        string? previousEventHash,
        string eventHash,
        string schemaVersion)
    {
        EventId = eventId;
        TenantId = tenantId;
        ActorId = actorId;
        ActorRole = actorRole;
        Action = action;
        ResourceType = resourceType;
        ResourceId = resourceId;
        Metadata = metadata;
        OccurredAt = occurredAt;
        PreviousEventHash = previousEventHash;
        EventHash = eventHash;
        SchemaVersion = schemaVersion;
    }

    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Unique event identifier (UUIDv7 for time-ordered Firestore storage).</summary>
    public string EventId { get; }

    /// <summary>Tenant that owns this event. Enforces multi-tenant audit isolation.</summary>
    public string TenantId { get; }

    // ── Actor ─────────────────────────────────────────────────────────────────

    /// <summary>Firebase UID of the authenticated user who performed the action.</summary>
    public string ActorId { get; }

    /// <summary>System role of the actor at the time of the action (e.g., "HRManager", "Director").</summary>
    public string ActorRole { get; }

    // ── What happened ─────────────────────────────────────────────────────────

    /// <summary>The type of action performed.</summary>
    public AuditAction Action { get; }

    /// <summary>The type of resource that was affected.</summary>
    public AuditResourceType ResourceType { get; }

    /// <summary>The Firestore document ID of the resource that was affected.</summary>
    public string ResourceId { get; }

    /// <summary>
    /// Optional JSON metadata specific to the action (e.g., names of changed fields).
    /// PII values must NEVER be stored here — use field names only, not field values.
    /// </summary>
    public string? Metadata { get; }

    // ── Timing ────────────────────────────────────────────────────────────────

    /// <summary>UTC timestamp when this event was recorded.</summary>
    public DateTimeOffset OccurredAt { get; }

    // ── Hash chain ────────────────────────────────────────────────────────────

    /// <summary>
    /// SHA-256 hex hash of the preceding event's canonical JSON.
    /// <c>null</c> or empty string for the genesis event (the very first event in the chain).
    /// </summary>
    public string? PreviousEventHash { get; }

    /// <summary>
    /// SHA-256 hex hash of this event's canonical JSON (computed at creation time).
    /// This value becomes the <see cref="PreviousEventHash"/> of the next event in the chain.
    /// </summary>
    public string EventHash { get; }

    // ── Versioning ────────────────────────────────────────────────────────────

    /// <summary>Canonical JSON schema version. Increment when the canonical format changes.</summary>
    public string SchemaVersion { get; }

    // ── Factory: Create (new event) ───────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="AuditEvent"/> and computes its SHA-256 hash.
    /// Use this when recording a new auditable action.
    /// </summary>
    /// <param name="tenantId">Tenant that owns this event.</param>
    /// <param name="actorId">Firebase UID of the actor.</param>
    /// <param name="actorRole">Role of the actor at the time of action.</param>
    /// <param name="action">The action performed.</param>
    /// <param name="resourceType">Type of resource affected.</param>
    /// <param name="resourceId">Firestore document ID of the affected resource.</param>
    /// <param name="metadata">Optional JSON metadata (field names only — no PII values).</param>
    /// <param name="occurredAt">When the action occurred (UTC).</param>
    /// <param name="previousEventHash">
    /// Hash of the preceding event, or <c>null</c> for the genesis event.
    /// </param>
    public static AuditEvent Create(
        string tenantId,
        string actorId,
        string actorRole,
        AuditAction action,
        AuditResourceType resourceType,
        string resourceId,
        string? metadata,
        DateTimeOffset occurredAt,
        string? previousEventHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorRole);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

        var eventId = Guid.CreateVersion7().ToString("D");

        // Build the event with a placeholder hash to compute the canonical JSON.
        // EventHash is NOT part of the canonical JSON, so the placeholder has no effect.
        var placeholder = new AuditEvent(
            eventId, tenantId, actorId, actorRole,
            action, resourceType, resourceId, metadata,
            occurredAt, previousEventHash,
            eventHash: string.Empty,
            schemaVersion: "1.0");

        var hash = ComputeHash(placeholder.ToCanonicalJson());

        return new AuditEvent(
            eventId, tenantId, actorId, actorRole,
            action, resourceType, resourceId, metadata,
            occurredAt, previousEventHash,
            eventHash: hash,
            schemaVersion: "1.0");
    }

    // ── Factory: Reconstitute (from Firestore) ────────────────────────────────

    /// <summary>
    /// Reconstitutes an <see cref="AuditEvent"/> from persisted Firestore data.
    /// Does NOT recompute the hash — call <see cref="VerifyHash"/> to validate integrity.
    /// </summary>
    public static AuditEvent Reconstitute(
        string eventId,
        string tenantId,
        string actorId,
        string actorRole,
        AuditAction action,
        AuditResourceType resourceType,
        string resourceId,
        string? metadata,
        DateTimeOffset occurredAt,
        string? previousEventHash,
        string eventHash,
        string schemaVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventHash);

        return new AuditEvent(
            eventId, tenantId, actorId, actorRole,
            action, resourceType, resourceId, metadata,
            occurredAt, previousEventHash, eventHash, schemaVersion);
    }

    // ── Hash computation ──────────────────────────────────────────────────────

    /// <summary>
    /// Builds the canonical JSON representation of this event for hashing.
    /// <para>
    /// <b>Immutability rule</b>: Field order and field names are FIXED.
    /// Never add, remove, or reorder fields here — doing so breaks all previously stored hashes.
    /// When the format must change, increment <see cref="SchemaVersion"/> and handle migration explicitly.
    /// </para>
    /// <para>Note: <see cref="EventHash"/> is excluded — it is the output, not an input to hashing.</para>
    /// </summary>
    public string ToCanonicalJson()
    {
        // Anonymous type with fixed field order.
        // System.Text.Json serialises anonymous type properties in declaration order.
        return JsonSerializer.Serialize(new
        {
            event_id = EventId,
            tenant_id = TenantId,
            actor_id = ActorId,
            actor_role = ActorRole,
            action = Action.ToString(),
            resource_type = ResourceType.ToString(),
            resource_id = ResourceId,
            metadata = Metadata,
            occurred_at = OccurredAt.ToString("o"),
            previous_event_hash = PreviousEventHash ?? string.Empty,
            schema_version = SchemaVersion,
        }, _canonicalJsonOptions);
    }

    /// <summary>
    /// Computes the SHA-256 hex hash of the given canonical JSON string.
    /// </summary>
    /// <param name="canonicalJson">Canonical JSON from <see cref="ToCanonicalJson"/>.</param>
    /// <returns>Lowercase hex-encoded SHA-256 hash (64 characters).</returns>
    public static string ComputeHash(string canonicalJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalJson);
        var bytes = Encoding.UTF8.GetBytes(canonicalJson);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Verifies that the stored <see cref="EventHash"/> matches the recomputed hash
    /// of this event's canonical JSON. A mismatch indicates tampering.
    /// </summary>
    /// <returns><c>true</c> if the hash is intact; <c>false</c> if the event has been tampered with.</returns>
    public bool VerifyHash()
    {
        var expected = ComputeHash(ToCanonicalJson());
        return string.Equals(EventHash, expected, StringComparison.OrdinalIgnoreCase);
    }
}
