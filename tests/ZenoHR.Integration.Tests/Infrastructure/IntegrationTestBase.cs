// REQ-OPS-003: Base class for all Firestore emulator integration tests.
// Provides Db access, a test-scoped tenant ID, and pre/post test data cleanup.

using Google.Cloud.Firestore;

namespace ZenoHR.Integration.Tests.Infrastructure;

/// <summary>
/// Base class for integration tests that use the Firestore emulator.
/// Each test class gets a unique tenant_id (UUID) so tests don't collide
/// even when run in parallel within the same emulator instance.
/// REQ-SEC-005: Tenant isolation is tested at the integration level.
/// REQ-OPS-003: All integration tests run against emulator.
/// </summary>
[Collection(EmulatorCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected FirestoreDb Db { get; }
    protected FirestoreEmulatorFixture Fixture { get; }

    /// <summary>
    /// Unique tenant ID per test class instance — prevents test cross-contamination.
    /// </summary>
    protected string TenantId { get; } = $"test-tenant-{Guid.NewGuid():N}";

    protected IntegrationTestBase(FirestoreEmulatorFixture fixture)
    {
        Fixture = fixture;
        Db = fixture.Db;
    }

    /// <summary>
    /// Called before each test method. Override to add per-test setup.
    /// </summary>
    public virtual Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Called after each test method. Override to add per-test teardown.
    /// Base implementation is intentionally empty — tenant-scoped data is
    /// cleaned between test class runs, not between individual test methods.
    /// </summary>
    public virtual Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Helper: write a document to a collection under the test tenant.
    /// Sets tenant_id automatically.
    /// </summary>
    protected async Task<DocumentReference> WriteTestDocumentAsync(
        string collection,
        string docId,
        Dictionary<string, object> fields)
    {
        var docRef = Db.Collection(collection).Document(docId);
        fields["tenant_id"] = TenantId;
        fields["created_at"] = Timestamp.GetCurrentTimestamp();
        await docRef.SetAsync(fields);
        return docRef;
    }

    /// <summary>
    /// Helper: read a document and assert it exists.
    /// </summary>
    protected async Task<DocumentSnapshot> GetExistingDocumentAsync(
        string collection, string docId)
    {
        var snap = await Db.Collection(collection).Document(docId).GetSnapshotAsync();
        if (!snap.Exists)
            throw new InvalidOperationException(
                $"Document {collection}/{docId} does not exist in emulator.");
        return snap;
    }
}
