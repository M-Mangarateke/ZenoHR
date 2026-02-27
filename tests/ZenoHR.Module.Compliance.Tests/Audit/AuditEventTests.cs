// TC-OPS-002: AuditEvent unit tests — hash computation, VerifyHash, and immutability guarantees.
// REQ-OPS-003: Every AuditEvent must carry a valid SHA-256 hash of its canonical JSON.
// REQ-OPS-004: Breaking the hash chain is a Sev-1 defect — these tests verify detection.

using FluentAssertions;
using ZenoHR.Module.Audit.Domain;

namespace ZenoHR.Module.Compliance.Tests.Audit;

public sealed class AuditEventTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AuditEvent CreateSampleEvent(string? previousHash = null) =>
        AuditEvent.Create(
            tenantId: "tenant-001",
            actorId: "user-firebase-uid",
            actorRole: "HRManager",
            action: AuditAction.Create,
            resourceType: AuditResourceType.Employee,
            resourceId: "emp-12345",
            metadata: null,
            occurredAt: new DateTimeOffset(2025, 3, 1, 8, 0, 0, TimeSpan.Zero),
            previousEventHash: previousHash);

    // ── Create factory ────────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidInputs_ReturnsEventWithNonEmptyHash()
    {
        var evt = CreateSampleEvent();

        evt.EventHash.Should().NotBeNullOrWhiteSpace();
        evt.EventHash.Should().HaveLength(64, "SHA-256 hex is always 64 characters");
    }

    [Fact]
    public void Create_ValidInputs_HashIsLowercaseHex()
    {
        var evt = CreateSampleEvent();

        evt.EventHash.Should().MatchRegex("^[0-9a-f]{64}$",
            "SHA-256 hex must be lowercase and 64 characters");
    }

    [Fact]
    public void Create_GenesisEvent_HasNullPreviousHash()
    {
        var evt = CreateSampleEvent(previousHash: null);

        evt.PreviousEventHash.Should().BeNull();
    }

    [Fact]
    public void Create_WithPreviousHash_StoresPreviousHash()
    {
        var first = CreateSampleEvent();
        var second = CreateSampleEvent(previousHash: first.EventHash);

        second.PreviousEventHash.Should().Be(first.EventHash);
    }

    [Fact]
    public void Create_TwiceWithSameInputs_ProducesDifferentEventIds()
    {
        // EventId uses UUIDv7 — monotonically increasing, never duplicate.
        var a = CreateSampleEvent();
        var b = CreateSampleEvent();

        a.EventId.Should().NotBe(b.EventId);
    }

    [Fact]
    public void Create_TwiceWithSameInputs_ProducesDifferentHashes()
    {
        // EventId differs between calls, so the canonical JSON differs, so the hash differs.
        var a = CreateSampleEvent();
        var b = CreateSampleEvent();

        a.EventHash.Should().NotBe(b.EventHash);
    }

    [Fact]
    public void Create_NullTenantId_Throws()
    {
        var act = () => AuditEvent.Create(
            tenantId: null!,
            actorId: "user",
            actorRole: "HRManager",
            action: AuditAction.Create,
            resourceType: AuditResourceType.Employee,
            resourceId: "emp-1",
            metadata: null,
            occurredAt: DateTimeOffset.UtcNow,
            previousEventHash: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyResourceId_Throws()
    {
        var act = () => AuditEvent.Create(
            tenantId: "t1",
            actorId: "u1",
            actorRole: "Director",
            action: AuditAction.Update,
            resourceType: AuditResourceType.Employee,
            resourceId: "   ",    // whitespace only
            metadata: null,
            occurredAt: DateTimeOffset.UtcNow,
            previousEventHash: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_SchemaVersionIs1_0()
    {
        var evt = CreateSampleEvent();

        evt.SchemaVersion.Should().Be("1.0");
    }

    [Fact]
    public void Create_WithMetadata_StoresMetadata()
    {
        var meta = "{\"changed_fields\":[\"salary\"]}";
        var evt = AuditEvent.Create(
            "t1", "u1", "HRManager",
            AuditAction.Update, AuditResourceType.EmploymentContract, "contract-1",
            metadata: meta,
            DateTimeOffset.UtcNow, null);

        evt.Metadata.Should().Be(meta);
    }

    // ── VerifyHash ────────────────────────────────────────────────────────────

    [Fact]
    public void VerifyHash_FreshEvent_ReturnsTrue()
    {
        var evt = CreateSampleEvent();

        evt.VerifyHash().Should().BeTrue();
    }

    [Fact]
    public void VerifyHash_ReconstitutedWithCorrectHash_ReturnsTrue()
    {
        var original = CreateSampleEvent();

        var reconstituted = AuditEvent.Reconstitute(
            original.EventId, original.TenantId, original.ActorId, original.ActorRole,
            original.Action, original.ResourceType, original.ResourceId, original.Metadata,
            original.OccurredAt, original.PreviousEventHash, original.EventHash, original.SchemaVersion);

        reconstituted.VerifyHash().Should().BeTrue();
    }

    [Fact]
    public void VerifyHash_ReconstitutedWithWrongHash_ReturnsFalse()
    {
        var original = CreateSampleEvent();

        var tampered = AuditEvent.Reconstitute(
            original.EventId, original.TenantId, original.ActorId, original.ActorRole,
            original.Action, original.ResourceType, original.ResourceId, original.Metadata,
            original.OccurredAt, original.PreviousEventHash,
            eventHash: "0000000000000000000000000000000000000000000000000000000000000000",  // wrong hash
            original.SchemaVersion);

        tampered.VerifyHash().Should().BeFalse();
    }

    // ── ToCanonicalJson ───────────────────────────────────────────────────────

    [Fact]
    public void ToCanonicalJson_SameInputs_ProducesSameJson()
    {
        var ts = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

        // Two events with the same EventId (via Reconstitute) and same inputs
        var evt1 = AuditEvent.Reconstitute(
            "evt-fixed-id", "tenant-1", "user-1", "HRManager",
            AuditAction.Create, AuditResourceType.Employee, "emp-1",
            null, ts, null, "placeholder-hash", "1.0");

        var evt2 = AuditEvent.Reconstitute(
            "evt-fixed-id", "tenant-1", "user-1", "HRManager",
            AuditAction.Create, AuditResourceType.Employee, "emp-1",
            null, ts, null, "placeholder-hash", "1.0");

        evt1.ToCanonicalJson().Should().Be(evt2.ToCanonicalJson());
    }

    [Fact]
    public void ToCanonicalJson_DifferentTenants_ProducesDifferentJson()
    {
        var ts = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

        var evt1 = AuditEvent.Reconstitute(
            "id", "tenant-A", "u1", "HRManager",
            AuditAction.Create, AuditResourceType.Employee, "r1",
            null, ts, null, "h", "1.0");

        var evt2 = AuditEvent.Reconstitute(
            "id", "tenant-B", "u1", "HRManager",
            AuditAction.Create, AuditResourceType.Employee, "r1",
            null, ts, null, "h", "1.0");

        evt1.ToCanonicalJson().Should().NotBe(evt2.ToCanonicalJson());
    }

    [Fact]
    public void ToCanonicalJson_GenesisEvent_ContainsEmptyStringForPreviousHash()
    {
        var evt = CreateSampleEvent(previousHash: null);
        var json = evt.ToCanonicalJson();

        json.Should().Contain("\"previous_event_hash\":\"\"",
            "null PreviousEventHash must serialise as empty string in canonical JSON");
    }

    [Fact]
    public void ToCanonicalJson_DoesNotContainEventHashField()
    {
        var evt = CreateSampleEvent();
        var json = evt.ToCanonicalJson();

        // Check for the exact JSON key "event_hash": — "previous_event_hash" is a different key
        json.Should().NotContain("\"event_hash\":",
            "EventHash must not be part of its own canonical JSON (it is the output, not an input)");
    }

    // ── ComputeHash ───────────────────────────────────────────────────────────

    [Fact]
    public void ComputeHash_SameInput_ProducesSameHash()
    {
        const string json = "{\"test\":\"value\"}";

        var hash1 = AuditEvent.ComputeHash(json);
        var hash2 = AuditEvent.ComputeHash(json);

        hash1.Should().Be(hash2, "SHA-256 is deterministic");
    }

    [Fact]
    public void ComputeHash_DifferentInputs_ProduceDifferentHashes()
    {
        var hash1 = AuditEvent.ComputeHash("{\"a\":1}");
        var hash2 = AuditEvent.ComputeHash("{\"a\":2}");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHash_KnownInput_MatchesExpectedSha256()
    {
        // SHA-256("hello") = 2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824
        const string expected = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";

        var hash = AuditEvent.ComputeHash("hello");

        hash.Should().Be(expected);
    }

    [Fact]
    public void ComputeHash_EmptyInput_Throws()
    {
        var act = () => AuditEvent.ComputeHash(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    // ── Reconstitute factory ──────────────────────────────────────────────────

    [Fact]
    public void Reconstitute_ValidInputs_PreservesAllFields()
    {
        var ts = new DateTimeOffset(2025, 1, 15, 9, 30, 0, TimeSpan.Zero);

        var evt = AuditEvent.Reconstitute(
            eventId: "evt-abc",
            tenantId: "t-xyz",
            actorId: "firebase-uid-001",
            actorRole: "Director",
            action: AuditAction.Finalize,
            resourceType: AuditResourceType.PayrollRun,
            resourceId: "run-2025-03",
            metadata: "{\"period\":\"March 2025\"}",
            occurredAt: ts,
            previousEventHash: "prevhash",
            eventHash: "anyhash",
            schemaVersion: "1.0");

        evt.EventId.Should().Be("evt-abc");
        evt.TenantId.Should().Be("t-xyz");
        evt.ActorId.Should().Be("firebase-uid-001");
        evt.ActorRole.Should().Be("Director");
        evt.Action.Should().Be(AuditAction.Finalize);
        evt.ResourceType.Should().Be(AuditResourceType.PayrollRun);
        evt.ResourceId.Should().Be("run-2025-03");
        evt.Metadata.Should().Be("{\"period\":\"March 2025\"}");
        evt.OccurredAt.Should().Be(ts);
        evt.PreviousEventHash.Should().Be("prevhash");
        evt.EventHash.Should().Be("anyhash");
        evt.SchemaVersion.Should().Be("1.0");
    }

    [Fact]
    public void Reconstitute_EmptyEventHash_Throws()
    {
        var act = () => AuditEvent.Reconstitute(
            "id", "t1", "u1", "HRManager",
            AuditAction.Read, AuditResourceType.Employee, "r1",
            null, DateTimeOffset.UtcNow, null,
            eventHash: "   ",   // whitespace
            "1.0");

        act.Should().Throw<ArgumentException>();
    }
}
