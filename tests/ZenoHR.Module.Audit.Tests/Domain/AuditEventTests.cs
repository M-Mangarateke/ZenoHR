// TC-AUDIT-001: AuditEvent — create, hash computation, VerifyHash, Reconstitute.
// REQ-OPS-003, CTL-POPIA-006: SHA-256 hash-chained immutable audit trail.
using FluentAssertions;
using ZenoHR.Module.Audit.Domain;

namespace ZenoHR.Module.Audit.Tests.Domain;

public sealed class AuditEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static AuditEvent ValidCreate(
        string? tenantId = "tenant_001",
        string? actorId = "actor_001",
        string? actorRole = "HRManager",
        AuditAction action = AuditAction.Create,
        AuditResourceType resourceType = AuditResourceType.Employee,
        string? resourceId = "emp_001",
        string? metadata = null,
        string? previousHash = null)
        => AuditEvent.Create(tenantId!, actorId!, actorRole!, action, resourceType, resourceId!,
            metadata, Now, previousHash);

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidInput_SetsAllProperties()
    {
        // TC-AUDIT-001-001
        var evt = ValidCreate();

        evt.TenantId.Should().Be("tenant_001");
        evt.ActorId.Should().Be("actor_001");
        evt.ActorRole.Should().Be("HRManager");
        evt.Action.Should().Be(AuditAction.Create);
        evt.ResourceType.Should().Be(AuditResourceType.Employee);
        evt.ResourceId.Should().Be("emp_001");
        evt.OccurredAt.Should().Be(Now);
        evt.SchemaVersion.Should().Be("1.0");
    }

    [Fact]
    public void Create_ComputesNonEmptyHash()
    {
        // TC-AUDIT-001-002
        var evt = ValidCreate();

        evt.EventHash.Should().NotBeNullOrEmpty();
        evt.EventHash.Should().HaveLength(64); // SHA-256 hex = 64 chars
    }

    [Fact]
    public void Create_GenesisEvent_HasNullPreviousHash()
    {
        // TC-AUDIT-001-003
        var evt = ValidCreate(previousHash: null);

        evt.PreviousEventHash.Should().BeNull();
    }

    [Fact]
    public void Create_WithPreviousHash_SetsPreviousHash()
    {
        // TC-AUDIT-001-004 — chained events
        var genesis = ValidCreate();
        var second = ValidCreate(previousHash: genesis.EventHash);

        second.PreviousEventHash.Should().Be(genesis.EventHash);
        second.EventHash.Should().NotBe(genesis.EventHash); // different events have different hashes
    }

    [Fact]
    public void Create_DifferentActions_ProduceDifferentHashes()
    {
        // TC-AUDIT-001-005 — each event's content is unique in the hash
        var create = ValidCreate(action: AuditAction.Create);
        var update = ValidCreate(action: AuditAction.Update);

        create.EventHash.Should().NotBe(update.EventHash);
    }

    [Fact]
    public void Create_WithMetadata_SetsMetadata()
    {
        // TC-AUDIT-001-006
        var evt = ValidCreate(metadata: "{\"fields\":[\"legal_name\"]}");

        evt.Metadata.Should().Be("{\"fields\":[\"legal_name\"]}");
    }

    [Fact]
    public void Create_EmptyTenantId_Throws()
    {
        // TC-AUDIT-001-007 — ArgumentException.ThrowIfNullOrWhiteSpace
        var act = () => ValidCreate(tenantId: "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyActorId_Throws()
    {
        // TC-AUDIT-001-008
        var act = () => ValidCreate(actorId: "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyResourceId_Throws()
    {
        // TC-AUDIT-001-009
        var act = () => ValidCreate(resourceId: "");

        act.Should().Throw<ArgumentException>();
    }

    // ── VerifyHash ────────────────────────────────────────────────────────────

    [Fact]
    public void VerifyHash_FreshEvent_ReturnsTrue()
    {
        // TC-AUDIT-001-010
        var evt = ValidCreate();

        evt.VerifyHash().Should().BeTrue();
    }

    [Fact]
    public void VerifyHash_TamperedReconstitution_ReturnsFalse()
    {
        // TC-AUDIT-001-011 — reconstitute with wrong hash simulates tampering
        var evt = ValidCreate();
        var tampered = AuditEvent.Reconstitute(
            evt.EventId, evt.TenantId, evt.ActorId, evt.ActorRole,
            evt.Action, evt.ResourceType, evt.ResourceId, evt.Metadata,
            evt.OccurredAt, evt.PreviousEventHash,
            "0000000000000000000000000000000000000000000000000000000000000000", // wrong hash
            evt.SchemaVersion);

        tampered.VerifyHash().Should().BeFalse();
    }

    // ── ToCanonicalJson ───────────────────────────────────────────────────────

    [Fact]
    public void ToCanonicalJson_IsStable_ForSameEvent()
    {
        // TC-AUDIT-001-020 — canonical JSON must be deterministic
        var evt = ValidCreate();
        var json1 = evt.ToCanonicalJson();
        var json2 = evt.ToCanonicalJson();

        json1.Should().Be(json2);
    }

    [Fact]
    public void ToCanonicalJson_ExcludesEventHash()
    {
        // TC-AUDIT-001-021 — EventHash must NOT appear in canonical JSON (it's the output)
        var evt = ValidCreate();
        var json = evt.ToCanonicalJson();

        json.Should().NotContain("\"event_hash\":");  // must not include own hash (used to compute it); previous_event_hash is allowed
    }

    // ── ComputeHash ───────────────────────────────────────────────────────────

    [Fact]
    public void ComputeHash_ProducesSHA256LowercaseHex()
    {
        // TC-AUDIT-001-030
        var hash = AuditEvent.ComputeHash("{\"test\":\"data\"}");

        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9a-f]+$"); // lowercase hex
    }

    [Fact]
    public void ComputeHash_SameInput_ProducesSameHash()
    {
        // TC-AUDIT-001-031 — SHA-256 is deterministic
        var h1 = AuditEvent.ComputeHash("hello");
        var h2 = AuditEvent.ComputeHash("hello");

        h1.Should().Be(h2);
    }

    [Fact]
    public void ComputeHash_DifferentInput_ProducesDifferentHash()
    {
        // TC-AUDIT-001-032
        var h1 = AuditEvent.ComputeHash("hello");
        var h2 = AuditEvent.ComputeHash("hello2");

        h1.Should().NotBe(h2);
    }

    // ── Reconstitute ──────────────────────────────────────────────────────────

    [Fact]
    public void Reconstitute_SetsAllProperties()
    {
        // TC-AUDIT-001-040
        var original = ValidCreate();
        var reconstituted = AuditEvent.Reconstitute(
            original.EventId, original.TenantId, original.ActorId, original.ActorRole,
            original.Action, original.ResourceType, original.ResourceId, original.Metadata,
            original.OccurredAt, original.PreviousEventHash, original.EventHash, original.SchemaVersion);

        reconstituted.EventId.Should().Be(original.EventId);
        reconstituted.EventHash.Should().Be(original.EventHash);
        reconstituted.VerifyHash().Should().BeTrue();
    }
}
