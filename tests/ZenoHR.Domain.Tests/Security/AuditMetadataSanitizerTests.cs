// TC-SEC-030: AuditEvent metadata sanitization — VUL-011 remediation tests.
// REQ-SEC-009, CTL-SEC-008: Validates JSON structure and strips HTML/script injection.
using FluentAssertions;
using ZenoHR.Infrastructure.Audit;

namespace ZenoHR.Domain.Tests.Security;

/// <summary>
/// Unit tests for <see cref="AuditMetadataSanitizer"/>.
/// Verifies that audit event metadata is sanitized before storage to prevent
/// XSS and log injection attacks.
/// VUL-011 remediation — TC-SEC-030
/// </summary>
public sealed class AuditMetadataSanitizerTests
{
    // TC-SEC-030-001: Valid JSON object passes through sanitization unchanged
    [Fact]
    public void Sanitize_ValidJson_ReturnsJson()
    {
        // Arrange
        const string validJson = """{"action":"employee_updated","fields":["LegalName","WorkEmail"]}""";

        // Act
        var result = AuditMetadataSanitizer.Sanitize(validJson);

        // Assert
        result.Should().NotBeNull(
            because: "valid JSON objects must be accepted as audit metadata");
        result.Should().Contain("employee_updated",
            because: "sanitized content must preserve legitimate metadata values");
    }

    // TC-SEC-030-002: HTML tags are stripped from metadata content
    [Fact]
    public void Sanitize_HtmlTags_StripsHtml()
    {
        // Arrange — metadata containing HTML tags (not script injection)
        const string jsonWithHtml = """{"note":"Updated <b>employee</b> record","fields":["LegalName"]}""";

        // Act
        var result = AuditMetadataSanitizer.Sanitize(jsonWithHtml);

        // Assert
        result.Should().NotBeNull(
            because: "metadata with stripped HTML should still be valid");
        result.Should().NotContain("<b>",
            because: "HTML tags must be stripped from audit metadata");
        result.Should().NotContain("</b>",
            because: "closing HTML tags must also be stripped");
        result.Should().Contain("employee",
            because: "text content outside HTML tags must be preserved");
    }

    // TC-SEC-030-003: Script tag injection is rejected entirely
    [Fact]
    public void Sanitize_ScriptTag_ReturnsNull()
    {
        // Arrange — metadata containing a script tag
        const string jsonWithScript = """{"note":"<script>alert('xss')</script>","fields":["LegalName"]}""";

        // Act
        var result = AuditMetadataSanitizer.Sanitize(jsonWithScript);

        // Assert
        result.Should().BeNull(
            because: "metadata containing script injection patterns must be rejected entirely");
    }

    // TC-SEC-030-004: Invalid JSON is rejected and returns null
    [Fact]
    public void Sanitize_InvalidJson_ReturnsNull()
    {
        // Arrange — malformed JSON
        const string invalidJson = """{"unclosed": "bracket""";

        // Act
        var result = AuditMetadataSanitizer.Sanitize(invalidJson);

        // Assert
        result.Should().BeNull(
            because: "invalid JSON must be rejected — audit metadata must always be parseable");
    }

    // TC-SEC-030-005: Null input returns null
    [Fact]
    public void Sanitize_NullInput_ReturnsNull()
    {
        // Act
        var result = AuditMetadataSanitizer.Sanitize(null);

        // Assert
        result.Should().BeNull(
            because: "null metadata is valid — not all audit events have metadata");
    }

    // TC-SEC-030-006: JSON arrays are rejected (only objects allowed)
    [Fact]
    public void Sanitize_JsonArray_ReturnsNull()
    {
        // Arrange — top-level array instead of object
        const string jsonArray = """["field1", "field2", "field3"]""";

        // Act
        var result = AuditMetadataSanitizer.Sanitize(jsonArray);

        // Assert
        result.Should().BeNull(
            because: "audit metadata must be a JSON object — arrays are not a valid metadata structure");
    }

    // TC-SEC-030-007: XSS attempt via event handler attribute is rejected
    [Fact]
    public void Sanitize_XssAttempt_ReturnsNull()
    {
        // Arrange — XSS via onerror attribute in tag
        const string xssAttempt = """{"note":"<img onerror=alert(1)>","action":"read"}""";

        // Act
        var result = AuditMetadataSanitizer.Sanitize(xssAttempt);

        // Assert
        result.Should().BeNull(
            because: "XSS injection via event handler attributes must be detected and rejected");
    }

    // TC-SEC-030-008: IsValid returns true for valid JSON objects
    [Fact]
    public void IsValid_ValidJson_ReturnsTrue()
    {
        // Arrange
        const string validJson = """{"changed_fields":["PersonalEmail","PhoneNumber"],"actor":"hr_manager"}""";

        // Act
        var result = AuditMetadataSanitizer.IsValid(validJson);

        // Assert
        result.Should().BeTrue(
            because: "IsValid must return true for well-formed JSON objects without injection patterns");
    }
}
