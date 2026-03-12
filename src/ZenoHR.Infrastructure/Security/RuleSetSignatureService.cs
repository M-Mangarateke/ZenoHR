// CTL-SARS-001: HMAC-SHA256 signature computation and verification for statutory rule sets.
// VUL-015: Statutory Rule Set Signature Verification — ensures PAYE tables are signed artifacts.
// REQ-HR-003: Payroll calculation rules must originate from verified, tamper-evident sources.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ZenoHR.Infrastructure.Security;

/// <summary>
/// Computes and verifies HMAC-SHA256 signatures for statutory rule set JSON content.
/// Provides canonical JSON serialization to ensure deterministic hashing regardless of
/// key ordering or whitespace in the source document.
/// CTL-SARS-001, VUL-015
/// </summary>
public sealed class RuleSetSignatureService
{
    /// <summary>
    /// Computes the HMAC-SHA256 of the given canonical JSON using the provided signing key.
    /// Returns the signature as a lowercase hex string.
    /// CTL-SARS-001
    /// </summary>
    /// <param name="canonicalJson">The canonicalized JSON string to sign.</param>
    /// <param name="signingKey">The HMAC-SHA256 key (must be at least 256 bits).</param>
    /// <returns>Lowercase hex-encoded HMAC-SHA256 signature.</returns>
    public static string ComputeSignature(string canonicalJson, byte[] signingKey)
    {
        ArgumentNullException.ThrowIfNull(canonicalJson);
        ArgumentNullException.ThrowIfNull(signingKey);

        var contentBytes = Encoding.UTF8.GetBytes(canonicalJson);
        var hashBytes = HMACSHA256.HashData(signingKey, contentBytes);

        return Convert.ToHexString(hashBytes).ToLower(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Verifies that the HMAC-SHA256 signature of the canonical JSON matches the expected value.
    /// Uses constant-time comparison to prevent timing side-channel attacks.
    /// CTL-SARS-001
    /// </summary>
    /// <param name="canonicalJson">The canonicalized JSON string to verify.</param>
    /// <param name="expectedSignature">The expected hex-encoded HMAC-SHA256 signature.</param>
    /// <param name="signingKey">The HMAC-SHA256 key.</param>
    /// <returns>True if the computed signature matches the expected signature.</returns>
    public static bool VerifySignature(string canonicalJson, string expectedSignature, byte[] signingKey)
    {
        ArgumentNullException.ThrowIfNull(canonicalJson);
        ArgumentNullException.ThrowIfNull(expectedSignature);
        ArgumentNullException.ThrowIfNull(signingKey);

        var computedSignature = ComputeSignature(canonicalJson, signingKey);

        // Constant-time comparison to prevent timing attacks.
        // Both strings are hex-encoded HMAC-SHA256 (64 chars each).
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(expectedSignature.ToLower(CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// Canonicalizes a JSON string by parsing it, sorting all object keys recursively,
    /// and serializing with no whitespace. This ensures deterministic hashing regardless
    /// of the original key order or formatting.
    /// CTL-SARS-001
    /// </summary>
    /// <param name="json">The JSON string to canonicalize.</param>
    /// <returns>A deterministic, minified JSON string with sorted keys.</returns>
    public static string Canonicalize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonical(writer, document.RootElement);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Convenience method: verifies a <see cref="SignedRuleSet"/> against a signing key.
    /// CTL-SARS-001, VUL-015
    /// </summary>
    public static RuleSetVerificationResult Verify(SignedRuleSet ruleSet, byte[] signingKey)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(signingKey);

        var canonical = Canonicalize(ruleSet.Content);
        var isValid = VerifySignature(canonical, ruleSet.Signature, signingKey);

        return new RuleSetVerificationResult(
            IsValid: isValid,
            RuleSetId: ruleSet.RuleSetId,
            Reason: isValid ? null : "HMAC-SHA256 signature mismatch — rule set content may have been tampered with.");
    }

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Recursively writes a <see cref="JsonElement"/> to a <see cref="Utf8JsonWriter"/>
    /// with object keys sorted in ordinal order. Arrays preserve element order.
    /// Numeric values are written using their raw text to preserve exact representation.
    /// </summary>
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                // Sort properties by key (ordinal) for deterministic output
                var properties = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    properties[property.Name] = property.Value;
                }
                foreach (var (name, value) in properties)
                {
                    writer.WritePropertyName(name);
                    WriteCanonical(writer, value);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText());
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;

            default:
                writer.WriteRawValue(element.GetRawText());
                break;
        }
    }
}
