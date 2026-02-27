// REQ-SEC-005: Tenant isolation enforced at repository level — every read and query filters by tenant_id.
// REQ-OPS-001: Base repository pattern — all Firestore data access flows through typed, tenant-scoped helpers.

using Google.Cloud.Firestore;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Infrastructure.Firestore;

/// <summary>
/// Abstract base for all Firestore repositories. Enforces:
/// <list type="bullet">
///   <item>Tenant isolation — reads verify tenant_id; queries are pre-filtered by tenant_id.</item>
///   <item>Write-once semantics — <see cref="CreateDocumentAsync"/> fails if the document already exists.</item>
///   <item>Typed <see cref="Result{T}"/> returns for expected business failures.</item>
/// </list>
/// Infrastructure exceptions (network errors, Firestore unavailable) propagate naturally
/// and are caught by the global exception handler in Program.cs.
/// </summary>
public abstract class BaseFirestoreRepository<T> where T : class
{
    protected FirestoreDb Db { get; }

    protected BaseFirestoreRepository(FirestoreDb db)
    {
        ArgumentNullException.ThrowIfNull(db);
        Db = db;
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
        var docRef = Collection.Document(documentId);
        var snapshot = await docRef.GetSnapshotAsync(ct);

        if (!snapshot.Exists)
            return Result<T>.Failure(ZenoHrError.NotFound(NotFoundErrorCode, CollectionName, documentId));

        // REQ-SEC-005: Reject documents that belong to a different tenant.
        if (snapshot.TryGetValue<string>("tenant_id", out var snapshotTenantId)
            && !string.Equals(snapshotTenantId, tenantId, StringComparison.Ordinal))
            return Result<T>.Failure(ZenoHrError.NotFound(NotFoundErrorCode, CollectionName, documentId));

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
        return snapshot.Documents.Select(FromSnapshot).ToList();
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
            return Result.Success();
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
        {
            return Result.Failure(
                ZenoHrErrorCode.FirestoreWriteConflict,
                $"{CollectionName}/{documentId} already exists — write-once invariant violated.");
        }
    }
}
