// REQ-OPS-003: Unit tests for EMP201 deadline calculation helpers.

using FluentAssertions;
using ZenoHR.Api.BackgroundServices;

namespace ZenoHR.Module.Compliance.Tests.BackgroundServices;

/// <summary>
/// Tests for <see cref="Emp201DeadlineHelper"/> — pure date-based deadline logic.
/// EMP201 is due by the 7th of each month for the previous month's PAYE/UIF/SDL.
/// </summary>
public sealed class Emp201DeadlineHelperTests
{
    // ── GetNextDeadline ──────────────────────────────────────────────────

    [Fact]
    public void GetNextDeadline_BeforeThe7th_ReturnsCurrentMonth7th()
    {
        // Arrange — 3 March 2026
        var today = new DateOnly(2026, 3, 3);

        // Act
        var result = Emp201DeadlineHelper.GetNextDeadline(today);

        // Assert
        result.Should().Be(new DateOnly(2026, 3, 7));
    }

    [Fact]
    public void GetNextDeadline_OnThe7th_ReturnsCurrentMonth7th()
    {
        // Arrange — 7 March 2026
        var today = new DateOnly(2026, 3, 7);

        // Act
        var result = Emp201DeadlineHelper.GetNextDeadline(today);

        // Assert
        result.Should().Be(new DateOnly(2026, 3, 7));
    }

    [Fact]
    public void GetNextDeadline_AfterThe7th_ReturnsNextMonth7th()
    {
        // Arrange — 8 March 2026
        var today = new DateOnly(2026, 3, 8);

        // Act
        var result = Emp201DeadlineHelper.GetNextDeadline(today);

        // Assert
        result.Should().Be(new DateOnly(2026, 4, 7));
    }

    [Fact]
    public void GetNextDeadline_December8th_ReturnsJanuary7thNextYear()
    {
        // Arrange — 8 December 2025
        var today = new DateOnly(2025, 12, 8);

        // Act
        var result = Emp201DeadlineHelper.GetNextDeadline(today);

        // Assert
        result.Should().Be(new DateOnly(2026, 1, 7));
    }

    // ── GetFilingPeriod ──────────────────────────────────────────────────

    [Fact]
    public void GetFilingPeriod_January7thDeadline_ReturnsDecemberPreviousYear()
    {
        // Arrange — deadline 7 January 2026 covers December 2025
        var deadline = new DateOnly(2026, 1, 7);

        // Act
        var (month, year) = Emp201DeadlineHelper.GetFilingPeriod(deadline);

        // Assert
        month.Should().Be(12);
        year.Should().Be(2025);
    }

    [Fact]
    public void GetFilingPeriod_March7thDeadline_ReturnsFebruary()
    {
        // Arrange — deadline 7 March 2026 covers February 2026
        var deadline = new DateOnly(2026, 3, 7);

        // Act
        var (month, year) = Emp201DeadlineHelper.GetFilingPeriod(deadline);

        // Assert
        month.Should().Be(2);
        year.Should().Be(2026);
    }

    // ── GetDaysUntilDeadline ─────────────────────────────────────────────

    [Fact]
    public void GetDaysUntilDeadline_5DaysBefore_Returns5()
    {
        // Arrange — 2 March 2026, deadline is 7 March
        var today = new DateOnly(2026, 3, 2);

        // Act
        var result = Emp201DeadlineHelper.GetDaysUntilDeadline(today);

        // Assert
        result.Should().Be(5);
    }

    [Fact]
    public void GetDaysUntilDeadline_OnDeadlineDay_Returns0()
    {
        // Arrange — 7 March 2026
        var today = new DateOnly(2026, 3, 7);

        // Act
        var result = Emp201DeadlineHelper.GetDaysUntilDeadline(today);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void GetDaysUntilDeadline_DayAfterDeadline_ReturnsDaysToNextMonth()
    {
        // Arrange — 8 March 2026, next deadline is 7 April = 30 days away
        var today = new DateOnly(2026, 3, 8);

        // Act
        var result = Emp201DeadlineHelper.GetDaysUntilDeadline(today);

        // Assert
        result.Should().Be(30); // March has 31 days, 8 Mar to 7 Apr = 30 days
    }

    // ── FormatFilingPeriod ───────────────────────────────────────────────

    [Fact]
    public void FormatFilingPeriod_SingleDigitMonth_PadsWithZero()
    {
        var result = Emp201DeadlineHelper.FormatFilingPeriod(2, 2026);

        result.Should().Be("02/2026");
    }

    [Fact]
    public void FormatFilingPeriod_December_FormatsCorrectly()
    {
        var result = Emp201DeadlineHelper.FormatFilingPeriod(12, 2025);

        result.Should().Be("12/2025");
    }
}
