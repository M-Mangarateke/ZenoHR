// REQ-SEC-005: Tenant isolation enforced at repository level — every read and query filters by tenant_id.
// REQ-OPS-001: Base repository pattern — all Firestore data access flows through typed, tenant-scoped helpers.
// REQ-OPS-005: Structured diagnostic logging for all Firestore operations (Debug) and security events (Warning).

using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Infrastructure.Firestore;

/// <summary>
/// Abstract base for all Firestore repositories. Enforces:
/// <list type="bullet">
///   <item>Tenant isolation — reads verify tenant_id; queries are pre-filtered by tenant_id.</item>
///   <item>Write-once semantics — <see cref="CreateDocumentAsync"/> fails if the document already exists.</item>
///   <item>Typed <see cref="Result{T}"/> returns for expected business failures.</item>
///   <item>Structured diagnostic logging — all ops at Debug, tenant violations and conflicts at Warning.</item>
/// </list>
/// Infrastructure exceptions (network errors, Firestore unavailable) propagate naturally
/// and are caught by the global exception handler in Program.cs.
/// </summary>
public abstract partial class BaseFirestoreRepository<T> where T : class
{
    protected FirestoreDb Db { get; }
    private readonly ILogger _logger;

    /// <summary>Logger instance for use in derived repositories.</summary>
    protected ILogger Logger => _logger;

    protected BaseFirestoreRepository(FirestoreDb db, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);
        Db = db;
        _logger = logger;
    }

    /// <summary>Firestore root collection name (e.g., "employees", "audit_events").</summary>
    protected abstract string CollectionName { get; }

    /// <summary>
    /// Error code returned when a document is not found or belongs to a different tenant.
    /// Override with the module-specific code (e.g., <see cref="ZenoHrErrorCode.EmployeeNotFound"/>).
    /// </summary>
    protected abstract ZenoHrErrorCode NotFoundErrorCode { get; }

    /// <summary>Hydrate a domain entity from a Firestore <see cref="DocumentSnapshot"/>.</summary>
    protected abstract T FromSnapshot(DocumentSnapshot snapshot);

    /// <summary>
    /// Serialize a domain entity to a Firestore field dictionary.
    /// All monetary values must be stored as strings (see MoneyZAR precision rules).
    /// </summary>
    protected abstract Dictionary<string, object?> ToDocument(T entity);

    /// <summary>Root collection reference for this repository.</summary>
    protected CollectionReference Collection => Db.Collection(CollectionName);

    // ── Reads ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetch a document by ID, verifying tenant isolation.
    /// Returns <see cref="NotFoundErrorCode"/> if absent or owned by a different tenant.
    /// </summary>
    protected async Task<Result<T>> GetByIdAsync(
        string tenantId, string documentId, CancellationToken ct = default)
    {
        LogRead(_logger, CollectionName, documentId);

        var docRef = Collection.Document(documentId);
        var snapshot = await docRef.GetSnapshotAsync(ct);

        if (!snapshot.Exists)
        {
            LogNotFound(_logger, CollectionName, documentId);
            return Result<T>.Failure(ZenoHrError.NotFound(NotFoundErrorCode, CollectionName, documentId));
        }

        // REQ-SEC-005: Reject documents that belong to a different tenant.
        if (snapshot.TryGetValue<string>("tenant_id", out var snapshotTenantId)
            && !string.Equals(snapshotTenantId, tenantId, StringComparison.Ordinal))
        {
            LogTenantViolation(_logger, CollectionName, documentId, tenantId);
            return Result<T>.Failure(ZenoHrError.NotFound(NotFoundErrorCode, CollectionName, documentId));
        }

        return Result<T>.Success(FromSnapshot(snapshot));
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Return a <see cref="Query"/> pre-filtered to <paramref name="tenantId"/>.
    /// Apply further constraints (WhereEqualTo, OrderBy, Limit) before executing.
    /// </summary>
    protected Query TenantQuery(string tenantId) =>
        Collection.WhereEqualTo("tenant_id", tenantId);

    /// <summary>Execute a query and hydrate all matching documents via <see cref="FromSnapshot"/>.</summary>
    protected async Task<IReadOnlyList<T>> ExecuteQueryAsync(Query query, CancellationToken ct = default)
    {
        var snapshot = await query.GetSnapshotAsync(ct);
        var results = snapshot.Documents.Select(FromSnapshot).ToList();
        LogQueryExecuted(_logger, CollectionName, results.Count);
        return results;
    }

    // ── Writes ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Create or overwrite a document (upsert).
    /// Use for mutable entities: Employee, EmploymentContract, LeaveRequest, etc.
    /// </summary>
    protected async Task<Result> SetDocumentAsync(
        string documentId, T entity, CancellationToken ct = default)
    {
        var docRef = Collection.Document(documentId);
        await docRef.SetAsync(ToDocument(entity), cancellationToken: ct);
        LogSet(_logger, CollectionName, documentId);
        return Result.Success();
    }

    /// <summary>
    /// Write-once create: fails with <see cref="ZenoHrErrorCode.FirestoreWriteConflict"/>
    /// if the document already exists.
    /// Use for immutable records: AuditEvent, AccrualLedgerEntry, finalised PayrollResult.
    /// </summary>
    protected async Task<Result> CreateDocumentAsync(
        string documentId, T entity, CancellationToken ct = default)
    {
        var docRef = Collection.Document(documentId);
        try
        {
            await docRef.CreateAsync(ToDocument(entity), ct);
            LogCreated(_logger, CollectionName, documentId);
            return Result.Success();
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
        {
            LogWriteConflict(_logger, CollectionName, documentId);
            return Result.Failure(
                ZenoHrErrorCode.FirestoreWriteConflict,
                $"{CollectionName}/{documentId} already exists — write-once invariant violated.");
        }
    }

    // ── Diagnostic logging (static source-generated — ILogger passed as first param) ──

    [LoggerMessage(EventId = 2000, Level = LogLevel.Debug, Message = "Read {Collection}/{DocumentId}")]
    private static partial void LogRead(ILogger logger, string collection, string documentId);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Debug, Message = "NotFound {Collection}/{DocumentId}")]
    private static partial void LogNotFound(ILogger logger, string collection, string documentId);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning,
        Message = "TenantViolation {Collection}/{DocumentId} — expected tenant {ExpectedTenant}")]
    private static partial void LogTenantViolation(ILogger logger, string collection, string documentId, string expectedTenant);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Debug, Message = "Query {Collection} → {Count} documents")]
    private static partial void LogQueryExecuted(ILogger logger, string collection, int count);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Debug, Message = "Set {Collection}/{DocumentId}")]
    private static partial void LogSet(ILogger logger, string collection, string documentId);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Debug, Message = "Create {Collection}/{DocumentId}")]
    private static partial void LogCreated(ILogger logger, string collection, string documentId);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Warning,
        Message = "WriteConflict {Collection}/{DocumentId} — write-once invariant violation")]
    private static partial void LogWriteConflict(ILogger logger, string collection, string documentId);
}
