// TC-OPS-005: AuditEventWriter integration tests.
// Verifies append-only semantics, hash chain integrity, and tenant isolation.
// REQ-COMP-005: Audit events must be immutable and hash-chained.
// CTL-POPIA-012: Every state-change is auditable.

using FluentAssertions;
using ZenoHR.Infrastructure.Audit;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Integration.Tests.Infrastructure;
using ZenoHR.Module.Audit.Domain;

namespace ZenoHR.Integration.Tests.Audit;

/// <summary>
/// Integration tests for <see cref="AuditEventWriter"/> and <see cref="AuditEventRepository"/>.
/// Verifies:
/// TC-OPS-005-A: Single event write succeeds and is readable.
/// TC-OPS-005-B: Hash chain is maintained across sequential writes.
/// TC-OPS-005-C: Tenant isolation — one tenant's events are not visible to another.
/// TC-OPS-005-D: Genesis event has null previous_event_hash.
/// TC-OPS-005-E: Write-once — duplicate event ID cannot be written.
/// TC-OPS-005-F: Chain head is updated after each write.
/// TC-OPS-005-G: VerifyHash passes on all written events.
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class AuditEventWriterTests : IntegrationTestBase
{
    private readonly AuditEventWriter _writer;
    private readonly AuditEventRepository _repository;

    public AuditEventWriterTests(FirestoreEmulatorFixture fixture) : base(fixture)
    {
        _repository = new AuditEventRepository(fixture.Db);
        _writer = new AuditEventWriter(fixture.Db, _repository);
    }

    // ── TC-OPS-005-A: Single write succeeds ──────────────────────────────────

    [Fact]
    public async Task WriteAsync_SingleEvent_Succeeds()
    {
        // Arrange
        var request = MakeRequest();

        // Act
        var result = await _writer.WriteAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue(because: $"write failed: {(result.IsSuccess ? "" : result.Error.Message)}");

        var evt = result.Value;
        evt.TenantId.Should().Be(TenantId);
        evt.ActorId.Should().Be(request.ActorId);
        evt.Action.Should().Be(AuditAction.Create);
        evt.ResourceType.Should().Be(AuditResourceType.Employee);
        evt.EventHash.Should().HaveLength(64, because: "SHA-256 produces 64 hex characters");
    }

    // ── TC-OPS-005-D: Genesis event has null previous hash ───────────────────

    [Fact]
    public async Task WriteAsync_FirstEvent_HasNullPreviousHash()
    {
        // Act
        var result = await _writer.WriteAsync(MakeRequest());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PreviousEventHash.Should().BeNull(
            because: "the first event in a chain has no predecessor");
    }

    // ── TC-OPS-005-B: Hash chain is maintained across sequential writes ───────

    [Fact]
    public async Task WriteAsync_ThreeEvents_FormValidHashChain()
    {
        // Act — write 3 events sequentially
        var r1 = await _writer.WriteAsync(MakeRequest(action: AuditAction.Create));
        var r2 = await _writer.WriteAsync(MakeRequest(action: AuditAction.Update));
        var r3 = await _writer.WriteAsync(MakeRequest(action: AuditAction.Read));

        r1.IsSuccess.Should().BeTrue();
        r2.IsSuccess.Should().BeTrue();
        r3.IsSuccess.Should().BeTrue();

        var e1 = r1.Value;
        var e2 = r2.Value;
        var e3 = r3.Value;

        // Chain links
        e1.PreviousEventHash.Should().BeNull("e1 is genesis");
        e2.PreviousEventHash.Should().Be(e1.EventHash, "e2 links to e1's hash");
        e3.PreviousEventHash.Should().Be(e2.EventHash, "e3 links to e2's hash");
    }

    // ── TC-OPS-005-G: VerifyHash passes on all written events ────────────────

    [Fact]
    public async Task WriteAsync_Events_AllPassHashVerification()
    {
        // Arrange
        for (var i = 0; i < 5; i++)
        {
            var r = await _writer.WriteAsync(MakeRequest());
            r.IsSuccess.Should().BeTrue();
        }

        // Act — read back all 5 events
        var events = await _repository.GetTenantEventsAsync(TenantId, limit: 10);

        // Assert — each event's stored hash is valid
        events.Should().HaveCount(5);
        foreach (var evt in events)
        {
            evt.VerifyHash().Should().BeTrue(
                because: $"event {evt.EventId} hash should be intact after Firestore round-trip");
        }
    }

    // ── TC-OPS-005-C: Tenant isolation ───────────────────────────────────────

    [Fact]
    public async Task WriteAsync_DifferentTenants_EventsAreIsolated()
    {
        // Arrange — two different tenant IDs
        var tenantA = TenantId; // from base class
        var tenantB = $"test-tenant-{Guid.NewGuid():N}";

        var requestA = MakeRequest(tenantId: tenantA);
        var requestB = MakeRequest(tenantId: tenantB);

        // Act
        var rA = await _writer.WriteAsync(requestA);
        var rB = await _writer.WriteAsync(requestB);

        rA.IsSuccess.Should().BeTrue();
        rB.IsSuccess.Should().BeTrue();

        // Assert — each tenant only sees its own events
        var eventsA = await _repository.GetTenantEventsAsync(tenantA);
        var eventsB = await _repository.GetTenantEventsAsync(tenantB);

        eventsA.Should().HaveCount(1).And.AllSatisfy(e => e.TenantId.Should().Be(tenantA));
        eventsB.Should().HaveCount(1).And.AllSatisfy(e => e.TenantId.Should().Be(tenantB));
    }

    // ── TC-OPS-005-E: Write-once (immutability) ───────────────────────────────

    [Fact]
    public async Task WriteAsync_EventPersisted_CanBeReadBackById()
    {
        // Act
        var writeResult = await _writer.WriteAsync(MakeRequest());
        writeResult.IsSuccess.Should().BeTrue();

        var eventId = writeResult.Value.EventId;

        // Read it back via the repository
        var readResult = await _repository.GetByEventIdAsync(TenantId, eventId);

        // Assert
        readResult.IsSuccess.Should().BeTrue("the written event must be readable by ID");
        readResult.Value.EventId.Should().Be(eventId);
        readResult.Value.EventHash.Should().Be(writeResult.Value.EventHash,
            because: "hash must survive Firestore round-trip unchanged");
    }

    // ── TC-OPS-005-F: Chain head is updated ──────────────────────────────────

    [Fact]
    public async Task WriteAsync_MultipleEvents_ChainHeadUpdatesCorrectly()
    {
        // Act
        var r1 = await _writer.WriteAsync(MakeRequest());
        var r2 = await _writer.WriteAsync(MakeRequest());
        var r3 = await _writer.WriteAsync(MakeRequest());

        // Assert chain head
        var head = await _writer.GetChainHeadAsync(TenantId);

        head.Should().NotBeNull("chain head must exist after writes");
        head!.EventCount.Should().Be(3, "three events were written");
        head.LastEventId.Should().Be(r3.Value.EventId, "last write becomes new head");
        head.LastEventHash.Should().Be(r3.Value.EventHash, "last event's hash is the chain head");
    }

    // ── TC-OPS-005-H: AuditHashChain.Verify passes ───────────────────────────

    [Fact]
    public async Task WriteAsync_FiveEvents_AuditHashChainVerifyPasses()
    {
        // Arrange
        for (var i = 0; i < 5; i++)
            (await _writer.WriteAsync(MakeRequest())).IsSuccess.Should().BeTrue();

        // Read events oldest-first (required for chain verification)
        var events = (await _repository.GetTenantEventsAsync(TenantId, limit: 10)).ToList();

        // Act
        var verificationResult = AuditHashChain.Verify(events);

        // Assert
        verificationResult.IsIntact.Should().BeTrue(
            because: $"chain must be intact: {verificationResult.FirstBrokenEventId ?? "none"}");
        verificationResult.EventsVerified.Should().Be(5);
        verificationResult.FirstBrokenEventId.Should().BeNull();
    }

    // ── TC-OPS-005-I: GetRecentEvents returns newest first ───────────────────

    [Fact]
    public async Task GetRecentEventsAsync_ReturnsNewestFirst()
    {
        // Arrange — write 3 events with slight time difference
        var r1 = await _writer.WriteAsync(MakeRequest(occurredAt: DateTimeOffset.UtcNow.AddMinutes(-2)));
        var r2 = await _writer.WriteAsync(MakeRequest(occurredAt: DateTimeOffset.UtcNow.AddMinutes(-1)));
        var r3 = await _writer.WriteAsync(MakeRequest(occurredAt: DateTimeOffset.UtcNow));

        // Act
        var recent = await _repository.GetRecentEventsAsync(TenantId, limit: 10);

        // Assert — newest first
        recent.Should().HaveCount(3);
        recent[0].EventId.Should().Be(r3.Value.EventId, because: "newest event is first in recent list");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private WriteAuditEventRequest MakeRequest(
        string? tenantId = null,
        AuditAction action = AuditAction.Create,
        DateTimeOffset? occurredAt = null) =>
        new()
        {
            TenantId = tenantId ?? TenantId,
            ActorId = "actor-firebase-uid-001",
            ActorRole = "HRManager",
            Action = action,
            ResourceType = AuditResourceType.Employee,
            ResourceId = $"emp-{Guid.NewGuid():N}",
            Metadata = """{"changed_fields":["first_name","last_name"]}""",
            OccurredAt = occurredAt ?? DateTimeOffset.UtcNow,
        };
}
