// TC-OPS-003: AuditHashChain unit tests — chain integrity verification.
// REQ-OPS-004: A broken chain must be detected at the first tampered event.
// CTL-POPIA-013: Tamper-evident audit log must pass chain verification on demand.

using FluentAssertions;
using ZenoHR.Module.Audit.Domain;

namespace ZenoHR.Module.Compliance.Tests.Audit;

public sealed class AuditHashChainTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AuditEvent MakeEvent(
        string tenantId = "t1",
        AuditAction action = AuditAction.Create,
        string resourceId = "r1",
        string? previousHash = null,
        DateTimeOffset? occurredAt = null) =>
        AuditEvent.Create(
            tenantId, "actor-1", "HRManager",
            action, AuditResourceType.Employee, resourceId,
            metadata: null,
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            previousEventHash: previousHash);

    /// <summary>Build a valid chain of <paramref name="count"/> events.</summary>
    private static List<AuditEvent> BuildValidChain(int count, string tenantId = "t1")
    {
        var chain = new List<AuditEvent>(count);
        string? prevHash = null;
        for (var i = 0; i < count; i++)
        {
            var evt = MakeEvent(tenantId: tenantId, resourceId: $"r{i}", previousHash: prevHash);
            chain.Add(evt);
            prevHash = evt.EventHash;
        }
        return chain;
    }

    // ── Empty and single-event chains ─────────────────────────────────────────

    [Fact]
    public void Verify_EmptyList_IsIntactWithZeroEventsVerified()
    {
        var result = AuditHashChain.Verify([]);

        result.IsIntact.Should().BeTrue();
        result.EventsVerified.Should().Be(0);
        result.FirstBrokenEventId.Should().BeNull();
        result.FirstBrokenIndex.Should().BeNull();
    }

    [Fact]
    public void Verify_SingleGenesisEvent_IsIntact()
    {
        var genesis = MakeEvent(previousHash: null);

        var result = AuditHashChain.Verify([genesis]);

        result.IsIntact.Should().BeTrue();
        result.EventsVerified.Should().Be(1);
        result.FirstBrokenEventId.Should().BeNull();
    }

    [Fact]
    public void Verify_NullList_Throws()
    {
        var act = () => AuditHashChain.Verify(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Valid chains ──────────────────────────────────────────────────────────

    [Fact]
    public void Verify_TwoEventChain_IsIntact()
    {
        var chain = BuildValidChain(2);

        var result = AuditHashChain.Verify(chain);

        result.IsIntact.Should().BeTrue();
        result.EventsVerified.Should().Be(2);
    }

    [Fact]
    public void Verify_TenEventChain_IsIntact()
    {
        var chain = BuildValidChain(10);

        var result = AuditHashChain.Verify(chain);

        result.IsIntact.Should().BeTrue();
        result.EventsVerified.Should().Be(10);
    }

    // ── Genesis event violations ───────────────────────────────────────────────

    [Fact]
    public void Verify_GenesisEventHasPreviousHash_BreaksAtIndex0()
    {
        // Force genesis to have a PreviousEventHash (should never happen in production)
        var wrongGenesis = MakeEvent(previousHash: "some-hash-that-should-not-be-here");

        var result = AuditHashChain.Verify([wrongGenesis]);

        result.IsIntact.Should().BeFalse();
        result.FirstBrokenIndex.Should().Be(0);
        result.FirstBrokenEventId.Should().Be(wrongGenesis.EventId);
        result.EventsVerified.Should().Be(0);
    }

    // ── Tampered event hash ────────────────────────────────────────────────────

    [Fact]
    public void Verify_FirstEventHashTampered_BreaksAtIndex0()
    {
        var chain = BuildValidChain(3);

        // Reconstitute the first event with a corrupted hash
        var original = chain[0];
        var tampered = AuditEvent.Reconstitute(
            original.EventId, original.TenantId, original.ActorId, original.ActorRole,
            original.Action, original.ResourceType, original.ResourceId, original.Metadata,
            original.OccurredAt, original.PreviousEventHash,
            eventHash: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            original.SchemaVersion);

        chain[0] = tampered;

        var result = AuditHashChain.Verify(chain);

        result.IsIntact.Should().BeFalse();
        result.FirstBrokenIndex.Should().Be(0);
        result.FirstBrokenEventId.Should().Be(tampered.EventId);
    }

    [Fact]
    public void Verify_MiddleEventHashTampered_BreaksAtCorrectIndex()
    {
        var chain = BuildValidChain(5);

        // Tamper with event at index 2
        var original = chain[2];
        var tampered = AuditEvent.Reconstitute(
            original.EventId, original.TenantId, original.ActorId, original.ActorRole,
            original.Action, original.ResourceType, original.ResourceId, original.Metadata,
            original.OccurredAt, original.PreviousEventHash,
            eventHash: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            original.SchemaVersion);

        chain[2] = tampered;

        var result = AuditHashChain.Verify(chain);

        result.IsIntact.Should().BeFalse();
        result.FirstBrokenIndex.Should().Be(2);
        result.FirstBrokenEventId.Should().Be(tampered.EventId);
        result.EventsVerified.Should().Be(2, "events 0 and 1 passed before the break was detected");
    }

    [Fact]
    public void Verify_LastEventHashTampered_BreaksAtLastIndex()
    {
        var chain = BuildValidChain(4);
        var lastIndex = chain.Count - 1;

        var original = chain[lastIndex];
        var tampered = AuditEvent.Reconstitute(
            original.EventId, original.TenantId, original.ActorId, original.ActorRole,
            original.Action, original.ResourceType, original.ResourceId, original.Metadata,
            original.OccurredAt, original.PreviousEventHash,
            eventHash: "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
            original.SchemaVersion);

        chain[lastIndex] = tampered;

        var result = AuditHashChain.Verify(chain);

        result.IsIntact.Should().BeFalse();
        result.FirstBrokenIndex.Should().Be(lastIndex);
        result.EventsVerified.Should().Be(lastIndex);
    }

    // ── Broken chain link (PreviousEventHash mismatch) ────────────────────────

    [Fact]
    public void Verify_PreviousHashMismatch_BreaksAtMismatchedEvent()
    {
        var chain = BuildValidChain(4);

        // Replace event at index 2 with one that has a wrong PreviousEventHash
        // (correct hash of its own data, but wrong link to prior event)
        var replacementEvt = AuditEvent.Create(
            "t1", "actor-99", "Director",
            AuditAction.Delete, AuditResourceType.Employee, "injected-resource",
            metadata: null,
            DateTimeOffset.UtcNow,
            previousEventHash: "wrong-previous-hash-does-not-match-event-1");

        chain[2] = replacementEvt;

        var result = AuditHashChain.Verify(chain);

        result.IsIntact.Should().BeFalse();
        result.FirstBrokenIndex.Should().Be(2,
            "the chain link breaks at index 2 where PreviousEventHash does not match event[1].EventHash");
        result.EventsVerified.Should().Be(2);
    }

    // ── Cross-tenant guard (caller responsibility check) ──────────────────────

    [Fact]
    public void Verify_EventsFromDifferentTenants_BreaksChainBecausePreviousHashMismatch()
    {
        // If events from different tenants are accidentally mixed, the chain breaks
        // because the second event's PreviousEventHash cannot match the first tenant's event hash.
        var tenant1Event = MakeEvent(tenantId: "tenant-A", previousHash: null);
        var tenant2Event = MakeEvent(tenantId: "tenant-B", previousHash: "completely-wrong-hash");

        var result = AuditHashChain.Verify([tenant1Event, tenant2Event]);

        result.IsIntact.Should().BeFalse("cross-tenant mixing breaks the chain at index 1");
        result.FirstBrokenIndex.Should().Be(1);
    }

    // ── EventsVerified semantics ──────────────────────────────────────────────

    [Fact]
    public void Verify_IntactChain_EventsVerifiedEqualsTotalCount()
    {
        var chain = BuildValidChain(7);

        var result = AuditHashChain.Verify(chain);

        result.EventsVerified.Should().Be(7);
    }

    [Fact]
    public void Verify_BrokenAtIndex3_EventsVerifiedIs3()
    {
        var chain = BuildValidChain(6);

        var original = chain[3];
        chain[3] = AuditEvent.Reconstitute(
            original.EventId, original.TenantId, original.ActorId, original.ActorRole,
            original.Action, original.ResourceType, original.ResourceId, original.Metadata,
            original.OccurredAt, original.PreviousEventHash,
            eventHash: "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
            original.SchemaVersion);

        var result = AuditHashChain.Verify(chain);

        result.EventsVerified.Should().Be(3,
            "events at indices 0, 1, 2 were verified before the break at index 3");
    }
}
