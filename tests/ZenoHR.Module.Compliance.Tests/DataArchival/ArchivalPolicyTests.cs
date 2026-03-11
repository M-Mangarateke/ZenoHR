// CTL-POPIA-009, CTL-POPIA-015, CTL-BCEA-004: Tests for data archival retention policy.

using FluentAssertions;
using ZenoHR.Module.Compliance.Services.DataArchival;

namespace ZenoHR.Module.Compliance.Tests.DataArchival;

public sealed class ArchivalPolicyTests
{
    // ── IsEligibleForArchival ────────────────────────────────────────────

    [Fact]
    public void IsEligibleForArchival_ExactlyFiveYears_ReturnsTrue()
    {
        // Arrange
        var terminationDate = new DateOnly(2020, 6, 15);
        var today = new DateOnly(2025, 6, 15); // Exactly 5 years later

        // Act
        var result = ArchivalPolicy.IsEligibleForArchival(terminationDate, today);

        // Assert
        result.Should().BeTrue("the retention period of 5 years has fully elapsed");
    }

    [Fact]
    public void IsEligibleForArchival_FourYearsElevenMonths_ReturnsFalse()
    {
        // Arrange
        var terminationDate = new DateOnly(2020, 6, 15);
        var today = new DateOnly(2025, 5, 15); // 4 years and 11 months

        // Act
        var result = ArchivalPolicy.IsEligibleForArchival(terminationDate, today);

        // Assert
        result.Should().BeFalse("the retention period has not yet elapsed");
    }

    [Fact]
    public void IsEligibleForArchival_SixYears_ReturnsTrue()
    {
        // Arrange
        var terminationDate = new DateOnly(2019, 1, 1);
        var today = new DateOnly(2025, 1, 1); // 6 years later

        // Act
        var result = ArchivalPolicy.IsEligibleForArchival(terminationDate, today);

        // Assert
        result.Should().BeTrue("6 years exceeds the 5-year retention period");
    }

    [Fact]
    public void IsEligibleForArchival_OneDayBeforeExpiry_ReturnsFalse()
    {
        // Arrange
        var terminationDate = new DateOnly(2020, 3, 1);
        var today = new DateOnly(2025, 2, 28); // One day before 5-year mark

        // Act
        var result = ArchivalPolicy.IsEligibleForArchival(terminationDate, today);

        // Assert
        result.Should().BeFalse("one day remains in the retention period");
    }

    [Fact]
    public void IsEligibleForArchival_OneDayAfterExpiry_ReturnsTrue()
    {
        // Arrange
        var terminationDate = new DateOnly(2020, 3, 1);
        var today = new DateOnly(2025, 3, 2); // One day after 5-year mark

        // Act
        var result = ArchivalPolicy.IsEligibleForArchival(terminationDate, today);

        // Assert
        result.Should().BeTrue("the retention period has elapsed by one day");
    }

    [Fact]
    public void IsEligibleForArchival_SameDay_ReturnsFalse()
    {
        // Arrange
        var terminationDate = new DateOnly(2025, 1, 15);
        var today = new DateOnly(2025, 1, 15); // Same day as termination

        // Act
        var result = ArchivalPolicy.IsEligibleForArchival(terminationDate, today);

        // Assert
        result.Should().BeFalse("no time has elapsed since termination");
    }

    [Fact]
    public void IsEligibleForArchival_LeapYearTermination_HandledCorrectly()
    {
        // Arrange — terminated on Feb 29 (leap year)
        var terminationDate = new DateOnly(2020, 2, 29);
        var expiryDate = new DateOnly(2025, 2, 28); // 2025 is not a leap year; AddYears rounds down
        var today = new DateOnly(2025, 2, 28);

        // Act
        var result = ArchivalPolicy.IsEligibleForArchival(terminationDate, today);

        // Assert — Feb 29 + 5 years = Feb 28 (non-leap year), so should be eligible on Feb 28
        result.Should().BeTrue("Feb 29 + 5 years resolves to Feb 28 in a non-leap year");
    }

    // ── GetRetentionExpiryDate ──────────────────────────────────────────

    [Fact]
    public void GetRetentionExpiryDate_ReturnsCorrectDate()
    {
        // Arrange
        var terminationDate = new DateOnly(2021, 7, 20);

        // Act
        var expiry = ArchivalPolicy.GetRetentionExpiryDate(terminationDate);

        // Assert
        expiry.Should().Be(new DateOnly(2026, 7, 20));
    }

    [Fact]
    public void GetRetentionExpiryDate_LeapYearDate_ReturnsCorrectDate()
    {
        // Arrange — leap year termination
        var terminationDate = new DateOnly(2020, 2, 29);

        // Act
        var expiry = ArchivalPolicy.GetRetentionExpiryDate(terminationDate);

        // Assert — 2025 is not a leap year, so Feb 29 + 5 years = Feb 28
        expiry.Should().Be(new DateOnly(2025, 2, 28));
    }

    // ── Constants / Invariants ──────────────────────────────────────────

    [Fact]
    public void RetentionYears_IsGreaterThanBceaMinimum()
    {
        // CTL-BCEA-004: BCEA requires 3-year minimum; our policy must exceed that.
        ArchivalPolicy.RetentionYears.Should().BeGreaterThan(ArchivalPolicy.BceaMinimumYears,
            "POPIA retention period must exceed the BCEA 3-year minimum");
    }

    [Fact]
    public void RetentionYears_IsFive()
    {
        ArchivalPolicy.RetentionYears.Should().Be(5);
    }

    [Fact]
    public void BceaMinimumYears_IsThree()
    {
        ArchivalPolicy.BceaMinimumYears.Should().Be(3);
    }
}
