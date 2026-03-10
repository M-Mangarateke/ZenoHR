// REQ-SEC-009, CTL-SEC-008: AuditEvent metadata sanitization.
// VUL-011 remediation: validates JSON structure and strips HTML/script tags.
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ZenoHR.Infrastructure.Audit;

/// <summary>
/// Sanitizes AuditEvent metadata before storage to prevent XSS and log injection.
/// VUL-011: Metadata field accepts unsanitized JSON; this sanitizer enforces structure.
/// </summary>
public static partial class AuditMetadataSanitizer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [GeneratedRegex(@"<[^>]*>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagPattern();

    // Matches script-related element names and event handler keywords inside HTML tags.
    // Checked BEFORE HTML stripping so <script> and onerror= are caught in original string.
    [GeneratedRegex(@"<\s*(script|iframe|object|embed|link)[^>]*>|on\w+\s*=",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ScriptPattern();

    /// <summary>
    /// Validates that metadata is valid JSON and strips HTML/script injection attempts.
    /// Returns sanitized JSON, or null if the metadata is invalid/dangerous.
    /// </summary>
    public static string? Sanitize(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata)) return null;

        // Validate JSON structure
        try
        {
            using var doc = JsonDocument.Parse(metadata);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null; // Only JSON objects are valid metadata
        }
        catch (JsonException)
        {
            return null; // Invalid JSON — reject
        }

        // Reject if script injection patterns present (check BEFORE stripping HTML)
        // Catches: <script>, <iframe>, event handlers like onerror= onclick=
        if (ScriptPattern().IsMatch(metadata))
            return null;

        // Strip remaining benign HTML tags (e.g., <b>, <em>, <i>)
        var sanitized = HtmlTagPattern().Replace(metadata, string.Empty);

        return sanitized;
    }

    /// <summary>Returns true if the metadata string is safe to store.</summary>
    public static bool IsValid(string? metadata) => Sanitize(metadata) != null;
}
