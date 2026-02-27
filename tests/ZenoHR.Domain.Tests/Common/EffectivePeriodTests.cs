// TC-HR-001: EffectivePeriod unit tests — verifies date-range membership, overlap, and factories.

using FluentAssertions;
using ZenoHR.Domain.Common;

namespace ZenoHR.Domain.Tests.Common;

public sealed class EffectivePeriodTests
{
    // ── Construction ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ClosedPeriod_SetsStartAndEnd()
    {
        var start = new DateOnly(2025, 3, 1);
        var end = new DateOnly(2026, 2, 28);
        var ep = new EffectivePeriod(start, end);

        ep.Start.Should().Be(start);
        ep.End.Should().Be(end);
    }

    [Fact]
    public void Constructor_OpenEnded_EndIsNull()
    {
        var ep = new EffectivePeriod(new DateOnly(2025, 1, 1));
        ep.End.Should().BeNull();
    }

    [Fact]
    public void Constructor_EndBeforeStart_Throws()
    {
        var act = () => new EffectivePeriod(new DateOnly(2025, 6, 1), new DateOnly(2025, 5, 31));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_SameDayStartAndEnd_IsValid()
    {
        var date = new DateOnly(2025, 3, 1);
        var ep = new EffectivePeriod(date, date);
        ep.Start.Should().Be(date);
        ep.End.Should().Be(date);
    }

    // ── Factory helpers ───────────────────────────────────────────────────────

    [Fact]
    public void OpenEndedFrom_CreatesOpenPeriod()
    {
        var ep = EffectivePeriod.OpenEndedFrom(new DateOnly(2020, 7, 1));
        ep.End.Should().BeNull();
    }

    [Fact]
    public void ForMonth_February2026_SetsCorrectBounds()
    {
        var ep = EffectivePeriod.ForMonth(2026, 2);
        ep.Start.Should().Be(new DateOnly(2026, 2, 1));
        ep.End.Should().Be(new DateOnly(2026, 2, 28));  // 2026 is not a leap year
    }

    [Fact]
    public void ForMonth_February2024_LeapYear_EndsOn29th()
    {
        var ep = EffectivePeriod.ForMonth(2024, 2);
        ep.End.Should().Be(new DateOnly(2024, 2, 29));
    }

    [Fact]
    public void ForMonth_March2025_SetsCorrectBounds()
    {
        var ep = EffectivePeriod.ForMonth(2025, 3);
        ep.Start.Should().Be(new DateOnly(2025, 3, 1));
        ep.End.Should().Be(new DateOnly(2025, 3, 31));
    }

    // ── Contains ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(2025, 3, 1)]   // first day
    [InlineData(2025, 6, 15)]  // mid-period
    [InlineData(2026, 2, 28)]  // last day
    public void Contains_DateWithinClosedPeriod_ReturnsTrue(int y, int m, int d)
    {
        var ep = new EffectivePeriod(new DateOnly(2025, 3, 1), new DateOnly(2026, 2, 28));
        ep.Contains(new DateOnly(y, m, d)).Should().BeTrue();
    }

    [Theory]
    [InlineData(2025, 2, 28)]  // day before start
    [InlineData(2026, 3, 1)]   // day after end
    public void Contains_DateOutsideClosedPeriod_ReturnsFalse(int y, int m, int d)
    {
        var ep = new EffectivePeriod(new DateOnly(2025, 3, 1), new DateOnly(2026, 2, 28));
        ep.Contains(new DateOnly(y, m, d)).Should().BeFalse();
    }

    [Fact]
    public void Contains_DateWithinOpenPeriod_ReturnsTrue()
    {
        var ep = EffectivePeriod.OpenEndedFrom(new DateOnly(2020, 1, 1));
        ep.Contains(new DateOnly(2099, 12, 31)).Should().BeTrue();
    }

    [Fact]
    public void Contains_DateBeforeOpenPeriodStart_ReturnsFalse()
    {
        var ep = EffectivePeriod.OpenEndedFrom(new DateOnly(2025, 1, 1));
        ep.Contains(new DateOnly(2024, 12, 31)).Should().BeFalse();
    }

    // ── Overlap ───────────────────────────────────────────────────────────────

    [Fact]
    public void Overlaps_AdjacentPeriods_ReturnsFalse()
    {
        // [Jan–Mar] and [Apr–Jun] do not overlap
        var a = new EffectivePeriod(new DateOnly(2025, 1, 1), new DateOnly(2025, 3, 31));
        var b = new EffectivePeriod(new DateOnly(2025, 4, 1), new DateOnly(2025, 6, 30));
        a.Overlaps(b).Should().BeFalse();
    }

    [Fact]
    public void Overlaps_OverlappingPeriods_ReturnsTrue()
    {
        // [Jan–Apr] and [Mar–Jun] share March and April
        var a = new EffectivePeriod(new DateOnly(2025, 1, 1), new DateOnly(2025, 4, 30));
        var b = new EffectivePeriod(new DateOnly(2025, 3, 1), new DateOnly(2025, 6, 30));
        a.Overlaps(b).Should().BeTrue();
        b.Overlaps(a).Should().BeTrue();  // symmetry
    }

    [Fact]
    public void Overlaps_OpenEndedAndClosed_ReturnsTrue()
    {
        var open = EffectivePeriod.OpenEndedFrom(new DateOnly(2024, 1, 1));
        var closed = new EffectivePeriod(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));
        open.Overlaps(closed).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_SameSingleDay_ReturnsTrue()
    {
        var day = new DateOnly(2025, 6, 15);
        var a = new EffectivePeriod(day, day);
        var b = new EffectivePeriod(day, day);
        a.Overlaps(b).Should().BeTrue();
    }

    // ── Equality ──────────────────────────────────────────────────────────────

    [Fact]
    public void Equality_SameStartAndEnd_AreEqual()
    {
        var a = new EffectivePeriod(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));
        var b = new EffectivePeriod(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));
        a.Equals(b).Should().BeTrue();
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentEnd_AreNotEqual()
    {
        var a = new EffectivePeriod(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));
        var b = new EffectivePeriod(new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 30));
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equality_OpenVsClosed_AreNotEqual()
    {
        var open = EffectivePeriod.OpenEndedFrom(new DateOnly(2025, 1, 1));
        var closed = new EffectivePeriod(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));
        open.Equals(closed).Should().BeFalse();
    }

    // ── ToString ──────────────────────────────────────────────────────────────

    [Fact]
    public void ToString_ClosedPeriod_ShowsBothDates()
    {
        var ep = new EffectivePeriod(new DateOnly(2025, 3, 1), new DateOnly(2026, 2, 28));
        ep.ToString().Should().Be("2025-03-01 to 2026-02-28");
    }

    [Fact]
    public void ToString_OpenPeriod_ShowsOpenEndedLabel()
    {
        var ep = EffectivePeriod.OpenEndedFrom(new DateOnly(2025, 3, 1));
        ep.ToString().Should().Be("2025-03-01 (open-ended)");
    }
}
