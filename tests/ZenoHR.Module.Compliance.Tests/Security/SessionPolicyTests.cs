// REQ-SEC-004: Tests for session timeout policy — VUL-013 idle timeout enforcement.
// TC-SEC-013: Validates privileged endpoint classification and session expiry logic.

using FluentAssertions;
using ZenoHR.Api.Auth;

namespace ZenoHR.Module.Compliance.Tests.Security;

/// <summary>
/// Unit tests for <see cref="SessionPolicy"/> — endpoint classification and idle timeout checks.
/// </summary>
public sealed class SessionPolicyTests
{
    // ── IsPrivilegedEndpoint ─────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/payroll/runs")]
    [InlineData("/api/payroll/adjustments")]
    [InlineData("/api/compliance/submissions")]
    [InlineData("/api/compliance/emp201")]
    [InlineData("/api/settings/statutory")]
    [InlineData("/api/settings/roles")]
    [InlineData("/api/efiling/emp201/submit")]
    [InlineData("/api/efiling/status")]
    public void IsPrivilegedEndpoint_PrivilegedPath_ReturnsTrue(string path)
    {
        // Act
        var result = SessionPolicy.IsPrivilegedEndpoint(path);

        // Assert
        result.Should().BeTrue(because: $"'{path}' is a privileged endpoint");
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/api/employees/123")]
    [InlineData("/api/leave/requests")]
    [InlineData("/api/leave/balances")]
    [InlineData("/api/clock/in")]
    [InlineData("/api/clock/out")]
    [InlineData("/api/clock/today")]
    [InlineData("/")]
    [InlineData("")]
    public void IsPrivilegedEndpoint_NonPrivilegedPath_ReturnsFalse(string path)
    {
        // Act
        var result = SessionPolicy.IsPrivilegedEndpoint(path);

        // Assert
        result.Should().BeFalse(because: $"'{path}' is not a privileged endpoint");
    }

    [Fact]
    public void IsPrivilegedEndpoint_CaseInsensitive_ReturnsTrue()
    {
        // Act
        var result = SessionPolicy.IsPrivilegedEndpoint("/API/PAYROLL/runs");

        // Assert
        result.Should().BeTrue(because: "path matching should be case-insensitive");
    }

    [Fact]
    public void IsPrivilegedEndpoint_NullPath_ThrowsArgumentNullException()
    {
        // Act
        var act = () => SessionPolicy.IsPrivilegedEndpoint(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // ── IsSessionExpired (privileged) ────────────────────────────────────────

    [Fact]
    public void IsSessionExpired_PrivilegedAfter16Minutes_ReturnsTrue()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var lastActivity = now.AddMinutes(-16);

        // Act
        var result = SessionPolicy.IsSessionExpired(lastActivity, isPrivileged: true, utcNow: now);

        // Assert
        result.Should().BeTrue(because: "16 minutes exceeds the 15-minute privileged timeout");
    }

    [Fact]
    public void IsSessionExpired_PrivilegedAfter14Minutes_ReturnsFalse()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var lastActivity = now.AddMinutes(-14);

        // Act
        var result = SessionPolicy.IsSessionExpired(lastActivity, isPrivileged: true, utcNow: now);

        // Assert
        result.Should().BeFalse(because: "14 minutes is within the 15-minute privileged timeout");
    }

    [Fact]
    public void IsSessionExpired_PrivilegedExactly15Minutes_ReturnsFalse()
    {
        // Arrange — exactly at the boundary should NOT expire (> not >=)
        var now = DateTimeOffset.UtcNow;
        var lastActivity = now.AddMinutes(-15);

        // Act
        var result = SessionPolicy.IsSessionExpired(lastActivity, isPrivileged: true, utcNow: now);

        // Assert
        result.Should().BeFalse(because: "exactly 15 minutes is not greater than the timeout");
    }

    [Fact]
    public void IsSessionExpired_PrivilegedAfter15MinutesAndOneSecond_ReturnsTrue()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var lastActivity = now.AddMinutes(-15).AddSeconds(-1);

        // Act
        var result = SessionPolicy.IsSessionExpired(lastActivity, isPrivileged: true, utcNow: now);

        // Assert
        result.Should().BeTrue(because: "15 minutes + 1 second exceeds the privileged timeout");
    }

    // ── IsSessionExpired (standard) ──────────────────────────────────────────

    [Fact]
    public void IsSessionExpired_StandardAfter61Minutes_ReturnsTrue()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var lastActivity = now.AddMinutes(-61);

        // Act
        var result = SessionPolicy.IsSessionExpired(lastActivity, isPrivileged: false, utcNow: now);

        // Assert
        result.Should().BeTrue(because: "61 minutes exceeds the 60-minute standard timeout");
    }

    [Fact]
    public void IsSessionExpired_StandardAfter59Minutes_ReturnsFalse()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var lastActivity = now.AddMinutes(-59);

        // Act
        var result = SessionPolicy.IsSessionExpired(lastActivity, isPrivileged: false, utcNow: now);

        // Assert
        result.Should().BeFalse(because: "59 minutes is within the 60-minute standard timeout");
    }

    [Fact]
    public void IsSessionExpired_StandardExactly60Minutes_ReturnsFalse()
    {
        // Arrange — exactly at the boundary should NOT expire (> not >=)
        var now = DateTimeOffset.UtcNow;
        var lastActivity = now.AddMinutes(-60);

        // Act
        var result = SessionPolicy.IsSessionExpired(lastActivity, isPrivileged: false, utcNow: now);

        // Assert
        result.Should().BeFalse(because: "exactly 60 minutes is not greater than the timeout");
    }

    [Fact]
    public void IsSessionExpired_StandardAfter30Minutes_ReturnsFalse()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var lastActivity = now.AddMinutes(-30);

        // Act
        var result = SessionPolicy.IsSessionExpired(lastActivity, isPrivileged: false, utcNow: now);

        // Assert
        result.Should().BeFalse(because: "30 minutes is well within the 60-minute standard timeout");
    }

    // ── Constants ────────────────────────────────────────────────────────────

    [Fact]
    public void PrivilegedIdleTimeoutMinutes_Is15()
    {
        SessionPolicy.PrivilegedIdleTimeoutMinutes.Should().Be(15);
    }

    [Fact]
    public void StandardIdleTimeoutMinutes_Is60()
    {
        SessionPolicy.StandardIdleTimeoutMinutes.Should().Be(60);
    }
}
