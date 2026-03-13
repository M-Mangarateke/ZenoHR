// VUL-010: Tests for key rotation policy configuration.

using FluentAssertions;
using ZenoHR.Infrastructure.Security;

namespace ZenoHR.Domain.Tests.Security;

public sealed class KeyRotationPolicyTests
{
    // ── Constants ──────────────────────────────────────────────────────────

    [Fact]
    public void RotationIntervalDays_Is180()
    {
        KeyRotationPolicy.RotationIntervalDays.Should().Be(180);
    }

    [Fact]
    public void WarningBeforeDays_Is30()
    {
        KeyRotationPolicy.WarningBeforeDays.Should().Be(30);
    }

    // ── IsRotationDue ─────────────────────────────────────────────────────

    [Fact]
    public void IsRotationDue_ExactlyAt180Days_ReturnsTrue()
    {
        var lastRotated = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var current = lastRotated.AddDays(180);

        KeyRotationPolicy.IsRotationDue(lastRotated, current).Should().BeTrue();
    }

    [Fact]
    public void IsRotationDue_At179Days_ReturnsFalse()
    {
        var lastRotated = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var current = lastRotated.AddDays(179);

        KeyRotationPolicy.IsRotationDue(lastRotated, current).Should().BeFalse();
    }

    [Fact]
    public void IsRotationDue_Overdue_ReturnsTrue()
    {
        var lastRotated = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var current = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero); // ~517 days

        KeyRotationPolicy.IsRotationDue(lastRotated, current).Should().BeTrue();
    }

    // ── GetNextRotationDate ───────────────────────────────────────────────

    [Fact]
    public void GetNextRotationDate_Returns180DaysLater()
    {
        var lastRotated = new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero);

        var next = KeyRotationPolicy.GetNextRotationDate(lastRotated);

        next.Should().Be(lastRotated.AddDays(180));
    }

    // ── IsInWarningPeriod ─────────────────────────────────────────────────

    [Fact]
    public void IsInWarningPeriod_At150Days_ReturnsTrue()
    {
        // Warning starts at 180 - 30 = 150 days
        var lastRotated = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var current = lastRotated.AddDays(150);

        KeyRotationPolicy.IsInWarningPeriod(lastRotated, current).Should().BeTrue();
    }

    [Fact]
    public void IsInWarningPeriod_At149Days_ReturnsFalse()
    {
        var lastRotated = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var current = lastRotated.AddDays(149);

        KeyRotationPolicy.IsInWarningPeriod(lastRotated, current).Should().BeFalse();
    }

    [Fact]
    public void IsInWarningPeriod_At180Days_ReturnsFalse()
    {
        // At exactly 180 days, rotation is due — no longer just a warning
        var lastRotated = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var current = lastRotated.AddDays(180);

        KeyRotationPolicy.IsInWarningPeriod(lastRotated, current).Should().BeFalse();
    }

    [Fact]
    public void IsInWarningPeriod_FreshKey_ReturnsFalse()
    {
        var lastRotated = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var current = lastRotated.AddDays(10);

        KeyRotationPolicy.IsInWarningPeriod(lastRotated, current).Should().BeFalse();
    }
}
