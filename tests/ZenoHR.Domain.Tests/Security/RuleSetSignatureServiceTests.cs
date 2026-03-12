// TC-SEC-040: RuleSetSignatureService — VUL-015 statutory rule set signature verification.
// CTL-SARS-001: Ensures PAYE/UIF/SDL/ETI tables are tamper-evident signed artifacts.
// REQ-HR-003: Payroll calculation rules must originate from verified sources.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using ZenoHR.Infrastructure.Security;

namespace ZenoHR.Domain.Tests.Security;

/// <summary>
/// Unit tests for <see cref="RuleSetSignatureService"/>.
/// Validates HMAC-SHA256 signature computation, verification, and JSON canonicalization.
/// VUL-015 remediation — TC-SEC-040
/// </summary>
public sealed class RuleSetSignatureServiceTests
{
    // 256-bit test key (64 hex chars = 32 bytes)
    private static readonly byte[] TestKey = Convert.FromHexString(
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");

    private static readonly byte[] AlternateKey = Convert.FromHexString(
        "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210");

    private const string SampleJson = """{"tax_year":2026,"brackets":[{"min":0,"max":237100,"rate":18}]}""";

    // ── ComputeSignature ───────────────────────────────────────────────────

    // TC-SEC-040-001: ComputeSignature returns consistent hash for same input
    [Fact]
    public void ComputeSignature_SameInput_ReturnsConsistentHash()
    {
        // Act
        var sig1 = RuleSetSignatureService.ComputeSignature(SampleJson, TestKey);
        var sig2 = RuleSetSignatureService.ComputeSignature(SampleJson, TestKey);

        // Assert
        sig1.Should().Be(sig2,
            because: "HMAC-SHA256 must be deterministic for the same input and key");
    }

    // TC-SEC-040-002: ComputeSignature returns lowercase hex string of correct length
    [Fact]
    public void ComputeSignature_ReturnsLowercaseHex64Chars()
    {
        // Act
        var signature = RuleSetSignatureService.ComputeSignature(SampleJson, TestKey);

        // Assert — HMAC-SHA256 produces 32 bytes = 64 hex chars
        signature.Should().HaveLength(64,
            because: "HMAC-SHA256 produces a 256-bit (32-byte) hash, encoded as 64 hex characters");
        signature.Should().MatchRegex("^[0-9a-f]{64}$",
            because: "the signature must be lowercase hexadecimal");
    }

    // TC-SEC-040-003: ComputeSignature matches known HMAC-SHA256 value
    [Fact]
    public void ComputeSignature_MatchesKnownHmac()
    {
        // Arrange — compute expected value independently
        var contentBytes = Encoding.UTF8.GetBytes(SampleJson);
        var expectedBytes = HMACSHA256.HashData(TestKey, contentBytes);
        var expected = Convert.ToHexString(expectedBytes).ToLower(CultureInfo.InvariantCulture);

        // Act
        var actual = RuleSetSignatureService.ComputeSignature(SampleJson, TestKey);

        // Assert
        actual.Should().Be(expected,
            because: "ComputeSignature must produce the standard HMAC-SHA256 output");
    }

    // TC-SEC-040-004: ComputeSignature with different key produces different hash
    [Fact]
    public void ComputeSignature_DifferentKey_ProducesDifferentHash()
    {
        // Act
        var sig1 = RuleSetSignatureService.ComputeSignature(SampleJson, TestKey);
        var sig2 = RuleSetSignatureService.ComputeSignature(SampleJson, AlternateKey);

        // Assert
        sig1.Should().NotBe(sig2,
            because: "different signing keys must produce different HMAC values");
    }

    // TC-SEC-040-005: ComputeSignature with different content produces different hash
    [Fact]
    public void ComputeSignature_DifferentContent_ProducesDifferentHash()
    {
        // Arrange
        const string altered = """{"tax_year":2027,"brackets":[{"min":0,"max":237100,"rate":18}]}""";

        // Act
        var sig1 = RuleSetSignatureService.ComputeSignature(SampleJson, TestKey);
        var sig2 = RuleSetSignatureService.ComputeSignature(altered, TestKey);

        // Assert
        sig1.Should().NotBe(sig2,
            because: "any content change must produce a completely different signature");
    }

    // ── VerifySignature ────────────────────────────────────────────────────

    // TC-SEC-040-006: VerifySignature returns true for valid signature
    [Fact]
    public void VerifySignature_ValidSignature_ReturnsTrue()
    {
        // Arrange
        var signature = RuleSetSignatureService.ComputeSignature(SampleJson, TestKey);

        // Act
        var result = RuleSetSignatureService.VerifySignature(SampleJson, signature, TestKey);

        // Assert
        result.Should().BeTrue(
            because: "a correctly computed signature must pass verification");
    }

    // TC-SEC-040-007: VerifySignature returns false for tampered content
    [Fact]
    public void VerifySignature_TamperedContent_ReturnsFalse()
    {
        // Arrange — sign the original, then verify with tampered content
        var signature = RuleSetSignatureService.ComputeSignature(SampleJson, TestKey);
        const string tampered = """{"tax_year":2026,"brackets":[{"min":0,"max":237100,"rate":19}]}""";

        // Act
        var result = RuleSetSignatureService.VerifySignature(tampered, signature, TestKey);

        // Assert
        result.Should().BeFalse(
            because: "tampered content must fail signature verification");
    }

    // TC-SEC-040-008: VerifySignature returns false for wrong key
    [Fact]
    public void VerifySignature_WrongKey_ReturnsFalse()
    {
        // Arrange — sign with TestKey, verify with AlternateKey
        var signature = RuleSetSignatureService.ComputeSignature(SampleJson, TestKey);

        // Act
        var result = RuleSetSignatureService.VerifySignature(SampleJson, signature, AlternateKey);

        // Assert
        result.Should().BeFalse(
            because: "verification with the wrong key must fail");
    }

    // TC-SEC-040-009: VerifySignature accepts uppercase hex signature (case-insensitive)
    [Fact]
    public void VerifySignature_UppercaseHex_ReturnsTrue()
    {
        // Arrange
        var signature = RuleSetSignatureService.ComputeSignature(SampleJson, TestKey);
        var uppercaseSig = signature.ToUpper(CultureInfo.InvariantCulture);

        // Act
        var result = RuleSetSignatureService.VerifySignature(SampleJson, uppercaseSig, TestKey);

        // Assert
        result.Should().BeTrue(
            because: "hex signature comparison should be case-insensitive");
    }

    // ── Canonicalize ───────────────────────────────────────────────────────

    // TC-SEC-040-010: Canonicalize produces deterministic output
    [Fact]
    public void Canonicalize_SameInput_ProducesSameOutput()
    {
        // Act
        var canon1 = RuleSetSignatureService.Canonicalize(SampleJson);
        var canon2 = RuleSetSignatureService.Canonicalize(SampleJson);

        // Assert
        canon1.Should().Be(canon2,
            because: "canonicalization must be deterministic");
    }

    // TC-SEC-040-011: Canonicalize with reordered keys produces same result
    [Fact]
    public void Canonicalize_ReorderedKeys_ProducesSameResult()
    {
        // Arrange — same data, different key order
        const string ordered = """{"alpha":1,"beta":2,"gamma":3}""";
        const string reversed = """{"gamma":3,"alpha":1,"beta":2}""";

        // Act
        var canon1 = RuleSetSignatureService.Canonicalize(ordered);
        var canon2 = RuleSetSignatureService.Canonicalize(reversed);

        // Assert
        canon1.Should().Be(canon2,
            because: "canonicalization must sort keys to produce identical output regardless of input order");
    }

    // TC-SEC-040-012: Canonicalize removes whitespace
    [Fact]
    public void Canonicalize_WithWhitespace_RemovesWhitespace()
    {
        // Arrange — pretty-printed JSON
        const string pretty = """
        {
            "tax_year": 2026,
            "rate":     18
        }
        """;

        // Act
        var canonical = RuleSetSignatureService.Canonicalize(pretty);

        // Assert
        canonical.Should().NotContain("\n",
            because: "canonical JSON must have no newlines");
        canonical.Should().NotContain("    ",
            because: "canonical JSON must have no indentation whitespace");
        canonical.Should().Be("""{"rate":18,"tax_year":2026}""",
            because: "keys must be sorted and whitespace removed");
    }

    // TC-SEC-040-013: Canonicalize handles nested objects with sorted keys
    [Fact]
    public void Canonicalize_NestedObjects_SortsKeysRecursively()
    {
        // Arrange
        const string nested = """{"outer":{"zebra":1,"apple":2},"inner":{"beta":"b","alpha":"a"}}""";

        // Act
        var canonical = RuleSetSignatureService.Canonicalize(nested);

        // Assert
        canonical.Should().Be("""{"inner":{"alpha":"a","beta":"b"},"outer":{"apple":2,"zebra":1}}""",
            because: "all keys at all nesting levels must be sorted");
    }

    // TC-SEC-040-014: Canonicalize preserves array order (arrays are ordered, not sorted)
    [Fact]
    public void Canonicalize_Arrays_PreservesOrder()
    {
        // Arrange
        const string json = """{"items":[3,1,2]}""";

        // Act
        var canonical = RuleSetSignatureService.Canonicalize(json);

        // Assert
        canonical.Should().Be("""{"items":[3,1,2]}""",
            because: "array element order must be preserved — arrays are positional, not key-sorted");
    }

    // TC-SEC-040-015: Canonicalize handles null, boolean, and string values
    [Fact]
    public void Canonicalize_MixedTypes_HandlesCorrectly()
    {
        // Arrange
        const string json = """{"z_null":null,"a_bool":true,"m_str":"hello","b_false":false}""";

        // Act
        var canonical = RuleSetSignatureService.Canonicalize(json);

        // Assert
        canonical.Should().Be("""{"a_bool":true,"b_false":false,"m_str":"hello","z_null":null}""",
            because: "canonicalization must handle all JSON value types and sort keys");
    }

    // ── Verify (SignedRuleSet convenience method) ──────────────────────────

    // TC-SEC-040-016: Verify returns valid result for correctly signed rule set
    [Fact]
    public void Verify_ValidSignedRuleSet_ReturnsValid()
    {
        // Arrange
        var canonical = RuleSetSignatureService.Canonicalize(SampleJson);
        var signature = RuleSetSignatureService.ComputeSignature(canonical, TestKey);
        var ruleSet = new SignedRuleSet(
            RuleSetId: "SARS_PAYE_2026",
            TaxYear: 2026,
            Content: SampleJson,
            Signature: signature,
            SignedAt: DateTimeOffset.UtcNow,
            SignedBy: "StatutoryRuleSetLoader");

        // Act
        var result = RuleSetSignatureService.Verify(ruleSet, TestKey);

        // Assert
        result.IsValid.Should().BeTrue(
            because: "a correctly signed rule set must pass verification");
        result.RuleSetId.Should().Be("SARS_PAYE_2026");
        result.Reason.Should().BeNull(
            because: "valid signatures have no failure reason");
    }

    // TC-SEC-040-017: Verify returns invalid result for tampered rule set
    [Fact]
    public void Verify_TamperedRuleSet_ReturnsInvalid()
    {
        // Arrange — sign original, then tamper with content
        var canonical = RuleSetSignatureService.Canonicalize(SampleJson);
        var signature = RuleSetSignatureService.ComputeSignature(canonical, TestKey);
        const string tampered = """{"tax_year":2026,"brackets":[{"min":0,"max":999999,"rate":99}]}""";
        var ruleSet = new SignedRuleSet(
            RuleSetId: "SARS_PAYE_2026",
            TaxYear: 2026,
            Content: tampered,
            Signature: signature,
            SignedAt: DateTimeOffset.UtcNow,
            SignedBy: "StatutoryRuleSetLoader");

        // Act
        var result = RuleSetSignatureService.Verify(ruleSet, TestKey);

        // Assert
        result.IsValid.Should().BeFalse(
            because: "tampered rule set content must fail verification");
        result.Reason.Should().Contain("tampered",
            because: "the failure reason should indicate potential tampering");
    }

    // TC-SEC-040-018: ComputeSignature throws on null input
    [Fact]
    public void ComputeSignature_NullJson_Throws()
    {
        // Act
        var act = () => RuleSetSignatureService.ComputeSignature(null!, TestKey);

        // Assert
        act.Should().Throw<ArgumentNullException>(
            because: "null canonical JSON is a programming error");
    }

    // TC-SEC-040-019: ComputeSignature throws on null key
    [Fact]
    public void ComputeSignature_NullKey_Throws()
    {
        // Act
        var act = () => RuleSetSignatureService.ComputeSignature(SampleJson, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>(
            because: "null signing key is a programming error");
    }

    // TC-SEC-040-020: Empty JSON object canonicalizes correctly
    [Fact]
    public void Canonicalize_EmptyObject_ReturnsEmptyObject()
    {
        // Act
        var canonical = RuleSetSignatureService.Canonicalize("{}");

        // Assert
        canonical.Should().Be("{}",
            because: "an empty JSON object should canonicalize to itself");
    }
}
