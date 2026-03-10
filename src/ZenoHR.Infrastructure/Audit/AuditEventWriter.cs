// REQ-COMP-005: AuditEventWriter — the single entry point for appending audit events.
// CTL-POPIA-012: Every PII access and state change must be recorded here.
// REQ-SEC-005: Hash chain must never be broken — atomic transaction enforces this invariant.
// REQ-OPS-005: Structured diagnostic logging for all chain write operations — EventIds 4000-4003.

using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using ZenoHR.Domain.Errors;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Module.Audit.Domain;

namespace ZenoHR.Infrastructure.Audit;

/// <summary>
/// Appends <see cref="AuditEvent"/> records to Firestore in an append-only, hash-chained manner.
/// <para>
/// Uses a Firestore transaction to atomically:
/// <list type="number">
///   <item>Read the current chain head for the tenant.</item>
///   <item>Create the new event with <c>previousEventHash</c> set to the last event's hash.</item>
///   <item>Write the event (write-once via <c>Transaction.Create</c>).</item>
///   <item>Update the <c>audit_chain_meta</c> chain head document.</item>
/// </list>
/// This guarantees the hash chain is never broken, even under concurrent writes.
/// </para>
/// </summary>
public sealed partial class AuditEventWriter
{
    // Separate collection for chain head pointers — one doc per tenant.
    // Avoids in-transaction queries (which have consistency edge cases).
    // Document ID: tenant_id
    private const string ChainMetaCollection = "audit_chain_meta";

    private readonly FirestoreDb _db;
    private readonly AuditEventRepository _repository;
    private readonly ILogger<AuditEventWriter> _logger;

    public AuditEventWriter(FirestoreDb db, AuditEventRepository repository,
        ILogger<AuditEventWriter> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Atomically appends a new <see cref="AuditEvent"/> to the tenant's hash chain.
    /// </summary>
    /// <param name="request">Details of the action to audit.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with the created <see cref="AuditEvent"/>, or failure if Firestore is unavailable
    /// or an extremely unlikely ID collision occurs.
    /// </returns>
    /// <remarks>
    /// Network errors and Firestore unavailability propagate as exceptions and are caught by the
    /// global exception handler. Only business failures are returned as <see cref="Result{T}"/>.
    /// </remarks>
    public async Task<Result<AuditEvent>> WriteAsync(
        WriteAuditEventRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        LogWriting(request.TenantId, request.Action, request.ResourceType, request.ResourceId);

        try
        {
            var auditEvent = await _db.RunTransactionAsync(async tx =>
            {
                // ── Step 1: Read the chain head for this tenant ──────────────
                var chainRef = _db.Collection(ChainMetaCollection).Document(request.TenantId);
                var chainSnap = await tx.GetSnapshotAsync(chainRef, ct);

                string? previousHash = null;
                long eventCount = 0;

                if (chainSnap.Exists)
                {
                    var rawHash = chainSnap.GetValue<string>("last_event_hash");
                    // Empty string means genesis (null) — stored as empty string for Firestore
                    previousHash = string.IsNullOrEmpty(rawHash) ? null : rawHash;
                    eventCount = chainSnap.GetValue<long>("event_count");
                }

                // ── Step 2: Create the new event (links to previous via hash) ─
                // VUL-011: Sanitize metadata before storage — strips HTML/script injection.
                // CTL-SEC-008: Only valid JSON objects with no XSS patterns are stored.
                var sanitizedMetadata = AuditMetadataSanitizer.Sanitize(request.Metadata);

                var evt = AuditEvent.Create(
                    tenantId: request.TenantId,
                    actorId: request.ActorId,
                    actorRole: request.ActorRole,
                    action: request.Action,
                    resourceType: request.ResourceType,
                    resourceId: request.ResourceId,
                    metadata: sanitizedMetadata,
                    occurredAt: request.OccurredAt,
                    previousEventHash: previousHash);

                // ── Step 3: Write the event (write-once — tx.Create fails if exists) ─
                // tx.Create is a Firestore precondition: document must not exist at commit time.
                // A UUIDv7 collision would trigger AlreadyExists — caught below.
                var eventRef = _repository.GetDocumentRef(evt.EventId);
                tx.Create(eventRef, _repository.BuildDocument(evt));

                // ── Step 4: Update the chain head ─────────────────────────────
                // tx.Set overwrites the chain meta document entirely (upsert).
                tx.Set(chainRef, new Dictionary<string, object?>
                {
                    ["tenant_id"] = request.TenantId,
                    ["last_event_hash"] = evt.EventHash,
                    ["last_event_id"] = evt.EventId,
                    // event_count allows detecting gaps without full chain scan
                    ["event_count"] = eventCount + 1,
                    ["updated_at"] = evt.OccurredAt,
                });

                return evt;
            }, cancellationToken: ct);

            LogWritten(auditEvent.EventId, auditEvent.EventHash.Length >= 8
                ? auditEvent.EventHash[..8] : auditEvent.EventHash);
            return Result<AuditEvent>.Success(auditEvent);
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
        {
            // tx.Create failed — UUIDv7 collision (effectively impossible, but handled for safety)
            LogIdCollision(request.TenantId, ex.Message[..Math.Min(ex.Message.Length, 100)]);
            return Result<AuditEvent>.Failure(
                ZenoHrErrorCode.FirestoreWriteConflict,
                $"Audit event ID collision detected (EventId). Details: {ex.Message}");
        }
        catch (Exception ex)
        {
            LogWriteFailed(ex, request.TenantId, request.Action);
            throw; // Re-throw — global exception handler will log full stack trace
        }
    }

    /// <summary>
    /// Returns the current chain head metadata for a tenant.
    /// Returns null if no events have been written yet (pre-genesis).
    /// Used by chain verification and diagnostics.
    /// </summary>
    public async Task<AuditChainHead?> GetChainHeadAsync(
        string tenantId, CancellationToken ct = default)
    {
        var chainRef = _db.Collection(ChainMetaCollection).Document(tenantId);
        var snap = await chainRef.GetSnapshotAsync(ct);

        if (!snap.Exists) return null;

        var rawHash = snap.GetValue<string>("last_event_hash");
        return new AuditChainHead(
            TenantId: tenantId,
            LastEventId: snap.GetValue<string>("last_event_id"),
            LastEventHash: string.IsNullOrEmpty(rawHash) ? null : rawHash,
            EventCount: snap.GetValue<long>("event_count"),
            UpdatedAt: snap.GetValue<Timestamp>("updated_at").ToDateTimeOffset());
    }

    // ── Diagnostic logging (source-generated, zero-allocation) ───────────────

    [LoggerMessage(EventId = 4000, Level = LogLevel.Debug,
        Message = "AuditEvent writing TenantId={TenantId} Action={Action} ResourceType={ResourceType} ResourceId={ResourceId}")]
    private partial void LogWriting(string tenantId, AuditAction action, AuditResourceType resourceType, string resourceId);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Debug,
        Message = "AuditEvent written EventId={EventId} HashPrefix={HashPrefix}")]
    private partial void LogWritten(string eventId, string hashPrefix);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Warning,
        Message = "AuditEvent ID collision TenantId={TenantId} Detail={Detail}")]
    private partial void LogIdCollision(string tenantId, string detail);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Error,
        Message = "AuditEvent write failed TenantId={TenantId} Action={Action}")]
    private partial void LogWriteFailed(Exception ex, string tenantId, AuditAction action);
}

/// <summary>Chain head metadata for a tenant's audit trail.</summary>
public sealed record AuditChainHead(
    string TenantId,
    string LastEventId,
    string? LastEventHash,
    long EventCount,
    DateTimeOffset UpdatedAt);
