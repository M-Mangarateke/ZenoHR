// TC-PAY-001: TaxYear unit tests — verifies SA tax year boundary calculations.
// Tax year runs 1 March (Y-1) to last day of February (Y).

using FluentAssertions;
using ZenoHR.Domain.Common;

namespace ZenoHR.Domain.Tests.Common;

public sealed class TaxYearTests
{
    // ── Construction ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_TaxYear2026_StartsOn1March2025()
    {
        var ty = new TaxYear(2026);
        ty.StartDate.Should().Be(new DateOnly(2025, 3, 1));
    }

    [Fact]
    public void Constructor_TaxYear2026_EndsOn28Feb2026()
    {
        var ty = new TaxYear(2026);
        ty.EndDate.Should().Be(new DateOnly(2026, 2, 28));
    }

    [Fact]
    public void Constructor_TaxYear2025_LeapYear_EndsOn29Feb2024()
    {
        // 2024 is a leap year, so 2024/2025 tax year ends 28 Feb 2025 (2025 is not leap)
        // 2023/2024 tax year ends 29 Feb 2024 (2024 IS leap)
        var ty = new TaxYear(2024);
        ty.EndDate.Should().Be(new DateOnly(2024, 2, 29)); // 2024 is a leap year
    }

    [Fact]
    public void Constructor_InvalidYear_ThrowsArgumentOutOfRange()
    {
        var act = () => new TaxYear(1999);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ── ForDate factory ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(2025, 3, 15, 2026)]  // March 2025 → 2025/2026 tax year
    [InlineData(2025, 12, 31, 2026)] // December 2025 → 2025/2026 tax year
    [InlineData(2026, 1, 15, 2026)]  // January 2026 → 2025/2026 tax year (Jan/Feb = same ending year)
    [InlineData(2026, 2, 28, 2026)]  // Last day Feb 2026 → 2025/2026 tax year
    [InlineData(2026, 3, 1, 2027)]   // 1 March 2026 → 2026/2027 tax year starts
    public void ForDate_ReturnsCorrectTaxYear(int year, int month, int day, int expectedEndingYear)
    {
        var date = new DateOnly(year, month, day);
        TaxYear.ForDate(date).EndingYear.Should().Be(expectedEndingYear);
    }

    // ── Contains ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(2025, 3, 1)]   // first day
    [InlineData(2025, 6, 15)]  // mid-year
    [InlineData(2026, 2, 28)]  // last day
    public void Contains_DateWithinTaxYear_ReturnsTrue(int y, int m, int d)
    {
        var ty = new TaxYear(2026);
        ty.Contains(new DateOnly(y, m, d)).Should().BeTrue();
    }

    [Theory]
    [InlineData(2025, 2, 28)]  // day before start
    [InlineData(2026, 3, 1)]   // day after end
    [InlineData(2024, 6, 1)]   // entirely before
    public void Contains_DateOutsideTaxYear_ReturnsFalse(int y, int m, int d)
    {
        var ty = new TaxYear(2026);
        ty.Contains(new DateOnly(y, m, d)).Should().BeFalse();
    }

    // ── Equality and comparison ───────────────────────────────────────────────

    [Fact]
    public void Equality_SameEndingYear_AreEqual()
    {
        new TaxYear(2026).Should().Be(new TaxYear(2026));
    }

    [Fact]
    public void Equality_DifferentEndingYear_AreNotEqual()
    {
        new TaxYear(2026).Should().NotBe(new TaxYear(2027));
    }

    [Fact]
    public void CompareTo_EarlierTaxYear_ReturnsNegative()
    {
        new TaxYear(2025).CompareTo(new TaxYear(2026)).Should().BeNegative();
    }

    // ── Display ───────────────────────────────────────────────────────────────

    [Fact]
    public void ToString_TaxYear2026_ReturnsSlashFormat()
    {
        new TaxYear(2026).ToString().Should().Be("2025/2026");
    }
}
