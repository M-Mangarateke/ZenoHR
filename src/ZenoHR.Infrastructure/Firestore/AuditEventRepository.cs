// REQ-COMP-005: Audit event repository — append-only reads and transaction support.
// CTL-POPIA-012: All PII access audit events are written via AuditEventWriter (uses this repo).
// REQ-SEC-005: Tenant isolation enforced — every read filters by tenant_id.

using Google.Cloud.Firestore;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Audit.Domain;

namespace ZenoHR.Infrastructure.Firestore;

/// <summary>
/// Repository for reading <see cref="AuditEvent"/> records from the Firestore
/// <c>audit_events</c> collection.
/// <para>
/// <b>Write rule</b>: Audit events are NEVER written directly through this repository.
/// All writes go through <c>AuditEventWriter</c>, which uses a Firestore transaction to
/// atomically maintain the hash chain. <see cref="ToDocument"/> is exposed internally so
/// <c>AuditEventWriter</c> can use it within a transaction.
/// </para>
/// </summary>
public sealed class AuditEventRepository : BaseFirestoreRepository<AuditEvent>
{
    public AuditEventRepository(FirestoreDb db) : base(db) { }

    protected override string CollectionName => "audit_events";
    protected override ZenoHrErrorCode NotFoundErrorCode => ZenoHrErrorCode.AuditEventNotFound;

    // ── Hydration ────────────────────────────────────────────────────────────

    protected override AuditEvent FromSnapshot(DocumentSnapshot snapshot)
    {
        // Parse action enum — unrecognised values fall back to Unknown (forward-compatibility)
        var actionStr = snapshot.GetValue<string>("action");
        var action = Enum.TryParse<AuditAction>(actionStr, out var a) ? a : AuditAction.Unknown;

        var resourceTypeStr = snapshot.GetValue<string>("resource_type");
        var resourceType = Enum.TryParse<AuditResourceType>(resourceTypeStr, out var rt)
            ? rt
            : AuditResourceType.Unknown;

        // previous_event_hash is stored as empty string for genesis events
        string? previousHash = null;
        if (snapshot.TryGetValue<string>("previous_event_hash", out var rawPrev)
            && !string.IsNullOrEmpty(rawPrev))
        {
            previousHash = rawPrev;
        }

        string? metadata = null;
        snapshot.TryGetValue<string>("metadata", out metadata);

        // occurred_at is stored as ISO 8601 string to guarantee exact round-trip for hash verification.
        // Firestore Timestamp precision loss (emulator or otherwise) would silently break VerifyHash().
        // ISO 8601 UTC strings are lexicographically sortable, preserving query order correctness.
        var occurredAtStr = snapshot.GetValue<string>("occurred_at");
        var occurredAt = DateTimeOffset.ParseExact(
            occurredAtStr, "o",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind);

        return AuditEvent.Reconstitute(
            eventId: snapshot.Id,
            tenantId: snapshot.GetValue<string>("tenant_id"),
            actorId: snapshot.GetValue<string>("actor_id"),
            actorRole: snapshot.GetValue<string>("actor_role"),
            action: action,
            resourceType: resourceType,
            resourceId: snapshot.GetValue<string>("resource_id"),
            metadata: metadata,
            occurredAt: occurredAt,
            previousEventHash: previousHash,
            eventHash: snapshot.GetValue<string>("event_hash"),
            schemaVersion: snapshot.GetValue<string>("schema_version"));
    }

    // ── Serialisation ────────────────────────────────────────────────────────

    protected override Dictionary<string, object?> ToDocument(AuditEvent entity) => new()
    {
        // REQ-SEC-005: tenant_id must be present on every document
        ["tenant_id"] = entity.TenantId,
        ["event_id"] = entity.EventId,
        ["actor_id"] = entity.ActorId,
        ["actor_role"] = entity.ActorRole,
        // Store enums as strings for human-readable Firestore documents
        ["action"] = entity.Action.ToString(),
        ["resource_type"] = entity.ResourceType.ToString(),
        ["resource_id"] = entity.ResourceId,
        // Metadata may be null — stored as Firestore null
        ["metadata"] = entity.Metadata,
        // IMPORTANT: occurred_at is stored as ISO 8601 string (not Firestore Timestamp).
        // This guarantees exact round-trip fidelity for hash computation.
        // Firestore emulator and SDK Timestamp precision loss would break VerifyHash() silently.
        // ISO 8601 UTC strings ("{o}" format) are lexicographically sortable — OrderBy still works.
        ["occurred_at"] = entity.OccurredAt.ToString("o"),
        // Genesis event has null PreviousEventHash — store as empty string for consistency
        ["previous_event_hash"] = entity.PreviousEventHash ?? string.Empty,
        ["event_hash"] = entity.EventHash,
        ["schema_version"] = entity.SchemaVersion,
    };

    // ── Internal helpers for AuditEventWriter (transaction writes) ────────────

    /// <summary>Returns the document reference for an audit event ID.</summary>
    internal DocumentReference GetDocumentRef(string eventId) =>
        Collection.Document(eventId);

    /// <summary>Serialises an <see cref="AuditEvent"/> to a Firestore dictionary.</summary>
    internal Dictionary<string, object?> BuildDocument(AuditEvent evt) =>
        ToDocument(evt);

    // ── Public reads ─────────────────────────────────────────────────────────

    /// <summary>
    /// Gets an audit event by its ID, verifying tenant ownership.
    /// Returns <see cref="ZenoHrErrorCode.AuditEventNotFound"/> if absent or wrong tenant.
    /// </summary>
    public Task<Result<AuditEvent>> GetByEventIdAsync(
        string tenantId, string eventId, CancellationToken ct = default)
        => GetByIdAsync(tenantId, eventId, ct);

    /// <summary>
    /// Returns up to <paramref name="limit"/> audit events for a tenant, ordered oldest-first.
    /// Used for hash-chain verification.
    /// CTL-POPIA-012
    /// </summary>
    public Task<IReadOnlyList<AuditEvent>> GetTenantEventsAsync(
        string tenantId, int limit = 100, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId)
            .OrderBy("occurred_at")
            .Limit(limit);
        return ExecuteQueryAsync(query, ct);
    }

    /// <summary>
    /// Returns the most recent audit events for a tenant, ordered newest-first.
    /// Useful for dashboard display.
    /// </summary>
    public Task<IReadOnlyList<AuditEvent>> GetRecentEventsAsync(
        string tenantId, int limit = 50, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId)
            .OrderByDescending("occurred_at")
            .Limit(limit);
        return ExecuteQueryAsync(query, ct);
    }
}
