// TC-OPS-001: Firestore emulator connectivity smoke test.
// Verifies that the emulator is reachable, read/write work, and tenant isolation holds.
// NOTE: These tests require the Firebase emulator to be running:
//   firebase emulators:start --only firestore,auth

using FluentAssertions;
using Google.Cloud.Firestore;
using ZenoHR.Integration.Tests.Infrastructure;

namespace ZenoHR.Integration.Tests;

/// <summary>
/// Smoke tests verifying the Firestore emulator fixture is properly configured.
/// TC-OPS-001: Emulator connectivity and basic CRUD.
/// TC-OPS-002: Tenant isolation — documents from one tenant not visible to another.
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class FirestoreEmulatorSmokeTests(FirestoreEmulatorFixture fixture)
    : IntegrationTestBase(fixture)
{
    // TC-OPS-001: Emulator write + read round-trip
    [Fact]
    public async Task WriteAndReadDocument_EmulatorRunning_RoundTripSucceeds()
    {
        // Arrange
        var docId = $"smoke-test-{Guid.NewGuid():N}";
        var data = new Dictionary<string, object>
        {
            ["name"] = "Smoke Test Employee",
            ["employee_number"] = "EMP-0001",
        };

        // Act
        await WriteTestDocumentAsync("employees", docId, data);
        var snapshot = await GetExistingDocumentAsync("employees", docId);

        // Assert
        snapshot.Exists.Should().BeTrue();
        snapshot.GetValue<string>("name").Should().Be("Smoke Test Employee");
        snapshot.GetValue<string>("tenant_id").Should().Be(TenantId);
    }

    // TC-OPS-002: Tenant isolation — query with different tenant_id returns no results
    [Fact]
    public async Task TenantQuery_DifferentTenantId_ReturnsNoDocuments()
    {
        // Arrange — write a doc under TenantId
        var docId = $"isolation-test-{Guid.NewGuid():N}";
        await WriteTestDocumentAsync("employees", docId, new Dictionary<string, object>
        {
            ["name"] = "Tenant A Employee"
        });

        // Act — query using a completely different tenant_id
        var differentTenantId = $"other-tenant-{Guid.NewGuid():N}";
        var query = Db.Collection("employees").WhereEqualTo("tenant_id", differentTenantId);
        var results = await query.GetSnapshotAsync();

        // Assert — different tenant cannot see documents from TenantId
        results.Documents.Should().NotContain(d => d.Id == docId,
            "cross-tenant document access must be impossible (REQ-SEC-005)");
    }

    // TC-OPS-003: Server timestamp is written correctly
    [Fact]
    public async Task WriteDocument_WithServerTimestamp_TimestampIsSet()
    {
        // Arrange
        var docId = $"timestamp-test-{Guid.NewGuid():N}";

        // Act
        await WriteTestDocumentAsync("audit_events", docId, new Dictionary<string, object>
        {
            ["event_type"] = "smoke_test",
            ["actor_id"] = "system",
        });

        var snapshot = await GetExistingDocumentAsync("audit_events", docId);

        // Assert — created_at set by WriteTestDocumentAsync is a valid timestamp
        var createdAt = snapshot.GetValue<Timestamp>("created_at");
        createdAt.ToDateTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }
}
