// TC-PAY-003: ETI calculation engine tests — tier determination, eligibility, and amount calculation.
// REQ-HR-003, CTL-SARS-003, PRD-16 Section 5.
// Rate band values verified against sars-eti.json seed data.

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using ZenoHR.Domain.Common;
using ZenoHR.Module.Payroll.Calculation;

namespace ZenoHR.Module.Payroll.Tests.Calculation;

/// <summary>
/// Tests for <see cref="EtiCalculationEngine"/> covering:
/// - <c>CalendarMonthsBetween</c> (PRD-16 Section 5 counting rule)
/// - <c>GetTier</c> (Tier1/Tier2/Ineligible)
/// - <c>IsEligible</c> (age, remuneration, tier guards)
/// - <c>CalculateMonthlyEti</c> (rate bands + hours proration)
/// - <c>ApplyEtiBands</c> (all three formula types)
/// </summary>
public sealed class EtiCalculationEngineTests
{
    // ── Test fixture ──────────────────────────────────────────────────────────

    /// <summary>
    /// Standard ETI rule set from sars-eti.json (Tier1 and Tier2 with 3 bands each).
    /// Tier1: 60%·remun / R1,500 flat / R1,500−0.75×(remun−5,500)
    /// Tier2: 30%·remun / R750 flat  / R750−0.375×(remun−5,500)
    /// </summary>
    private static SarsEtiRuleSet StandardRules() => SarsEtiRuleSet.CreateForTesting(
        tier1Bands: new List<EtiRateBand>
        {
            new() { MinRemuneration = 0m,      MaxRemuneration = 2_499.99m, FormulaType = "percentage", Rate = 0.60m },
            new() { MinRemuneration = 2_500m,  MaxRemuneration = 5_499.99m, FormulaType = "fixed",      FlatAmount = 1_500m },
            new() { MinRemuneration = 5_500m,  MaxRemuneration = 7_499.99m, FormulaType = "taper",      FlatAmount = 1_500m, TaperRate = 0.75m, TaperFloor = 5_500m },
        },
        tier2Bands: new List<EtiRateBand>
        {
            new() { MinRemuneration = 0m,      MaxRemuneration = 2_499.99m, FormulaType = "percentage", Rate = 0.30m },
            new() { MinRemuneration = 2_500m,  MaxRemuneration = 5_499.99m, FormulaType = "fixed",      FlatAmount = 750m },
            new() { MinRemuneration = 5_500m,  MaxRemuneration = 7_499.99m, FormulaType = "taper",      FlatAmount = 750m, TaperRate = 0.375m, TaperFloor = 5_500m },
        },
        ageMin: 18, ageMax: 29,
        minWage: 2_500m, maxRemuneration: 7_500m,
        standardHours: 160);

    // ── CalendarMonthsBetween ────────────────────────────────────────────────

    [Theory]
    [InlineData("2025-01-15", "2025-03-15", 2)] // PRD-16 Section 5 explicit example: 2 months
    [InlineData("2025-01-15", "2025-03-14", 1)] // PRD-16 Section 5 explicit example: 1 month (end day < start day)
    [InlineData("2025-01-01", "2026-01-01", 12)] // exactly 12 complete months
    [InlineData("2025-01-01", "2026-02-01", 13)] // 13 complete months
    [InlineData("2025-01-01", "2027-01-01", 24)] // exactly 24 months
    [InlineData("2025-01-01", "2027-01-02", 24)] // 24 months + 1 day = still 24
    [InlineData("2025-01-01", "2027-02-01", 25)] // 25 months → ineligible
    [InlineData("2025-03-31", "2025-04-30", 0)] // end day (30) < start day (31) → 0 months
    [InlineData("2025-03-31", "2025-05-01", 1)] // 1 month (Apr not complete, May 1 ≥ 31 threshold? no: 1<31 → 0? wait)
    public void CalendarMonthsBetween_Returns_CorrectMonthCount(
        string start, string end, int expectedMonths)
    {
        var startDate = DateOnly.Parse(start);
        var endDate   = DateOnly.Parse(end);
        var result = EtiCalculationEngine.CalendarMonthsBetween(startDate, endDate);
        result.Should().Be(expectedMonths);
    }

    [Fact]
    public void CalendarMonthsBetween_EndBeforeStart_ReturnsZero()
    {
        var result = EtiCalculationEngine.CalendarMonthsBetween(
            new DateOnly(2025, 6, 1), new DateOnly(2025, 1, 1));
        result.Should().Be(0);
    }

    [Fact]
    public void CalendarMonthsBetween_SameDate_ReturnsZero()
    {
        var date = new DateOnly(2025, 6, 15);
        EtiCalculationEngine.CalendarMonthsBetween(date, date).Should().Be(0);
    }

    // ── GetTier ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("2025-01-01", "2025-01-01", EtiTier.Ineligible)]  // 0 months → ineligible
    [InlineData("2025-01-01", "2025-02-01", EtiTier.Tier1)]       // 1 month → Tier1
    [InlineData("2025-01-01", "2026-01-01", EtiTier.Tier1)]       // 12 months → Tier1
    [InlineData("2025-01-01", "2026-02-01", EtiTier.Tier2)]       // 13 months → Tier2
    [InlineData("2025-01-01", "2027-01-01", EtiTier.Tier2)]       // 24 months → Tier2
    [InlineData("2025-01-01", "2027-02-01", EtiTier.Ineligible)]  // 25 months → ineligible
    [InlineData("2025-01-01", "2030-01-01", EtiTier.Ineligible)]  // far future → ineligible
    public void GetTier_ReturnsCorrectTierForMonthsEmployed(
        string start, string calcDate, EtiTier expected)
    {
        var result = EtiCalculationEngine.GetTier(DateOnly.Parse(start), DateOnly.Parse(calcDate));
        result.Should().Be(expected);
    }

    // ── IsEligible ────────────────────────────────────────────────────────────

    [Fact]
    public void IsEligible_ValidAge_ValidRemuneration_Tier1_ReturnsTrue()
    {
        var result = EtiCalculationEngine.IsEligible(
            ageAtCalculationDate: 25,
            monthlyRemuneration: new MoneyZAR(3_000m),
            tier: EtiTier.Tier1,
            rules: StandardRules());
        result.Should().BeTrue();
    }

    [Fact]
    public void IsEligible_AgeBelowMinimum_ReturnsFalse()
    {
        var result = EtiCalculationEngine.IsEligible(
            ageAtCalculationDate: 17,
            monthlyRemuneration: new MoneyZAR(3_000m),
            tier: EtiTier.Tier1,
            rules: StandardRules());
        result.Should().BeFalse();
    }

    [Fact]
    public void IsEligible_AgeAboveMaximum_ReturnsFalse()
    {
        var result = EtiCalculationEngine.IsEligible(
            ageAtCalculationDate: 30,
            monthlyRemuneration: new MoneyZAR(3_000m),
            tier: EtiTier.Tier1,
            rules: StandardRules());
        result.Should().BeFalse();
    }

    [Fact]
    public void IsEligible_AgeAtBoundaryMin_ReturnsTrue()
    {
        var result = EtiCalculationEngine.IsEligible(
            ageAtCalculationDate: 18,
            monthlyRemuneration: new MoneyZAR(3_000m),
            tier: EtiTier.Tier1,
            rules: StandardRules());
        result.Should().BeTrue();
    }

    [Fact]
    public void IsEligible_AgeAtBoundaryMax_ReturnsTrue()
    {
        var result = EtiCalculationEngine.IsEligible(
            ageAtCalculationDate: 29,
            monthlyRemuneration: new MoneyZAR(3_000m),
            tier: EtiTier.Tier1,
            rules: StandardRules());
        result.Should().BeTrue();
    }

    [Fact]
    public void IsEligible_RemunerationBelowMinWage_ReturnsFalse()
    {
        var result = EtiCalculationEngine.IsEligible(
            ageAtCalculationDate: 25,
            monthlyRemuneration: new MoneyZAR(2_499.99m),
            tier: EtiTier.Tier1,
            rules: StandardRules());
        result.Should().BeFalse();
    }

    [Fact]
    public void IsEligible_RemunerationAtMinWage_ReturnsTrue()
    {
        var result = EtiCalculationEngine.IsEligible(
            ageAtCalculationDate: 25,
            monthlyRemuneration: new MoneyZAR(2_500m),
            tier: EtiTier.Tier1,
            rules: StandardRules());
        result.Should().BeTrue();
    }

    [Fact]
    public void IsEligible_RemunerationAboveMaximum_ReturnsFalse()
    {
        var result = EtiCalculationEngine.IsEligible(
            ageAtCalculationDate: 25,
            monthlyRemuneration: new MoneyZAR(7_500.01m),
            tier: EtiTier.Tier1,
            rules: StandardRules());
        result.Should().BeFalse();
    }

    [Fact]
    public void IsEligible_IneligibleTier_ReturnsFalse()
    {
        var result = EtiCalculationEngine.IsEligible(
            ageAtCalculationDate: 25,
            monthlyRemuneration: new MoneyZAR(3_000m),
            tier: EtiTier.Ineligible,
            rules: StandardRules());
        result.Should().BeFalse();
    }

    // ── ApplyEtiBands — all three formula types ───────────────────────────────

    [Fact]
    public void ApplyEtiBands_PercentageBand_Returns60Percent()
    {
        // Tier1, band 1: 0–2499.99, formula = 0.60 × remun
        // R1,000 → 0.60 × 1000 = R600
        var result = EtiCalculationEngine.ApplyEtiBands(1_000m, StandardRules().Tier1Bands);
        result.Should().Be(600m);
    }

    [Fact]
    public void ApplyEtiBands_FixedBand_Tier1_Returns1500()
    {
        // Tier1, band 2: 2500–5499.99, formula = 1500 flat
        var result = EtiCalculationEngine.ApplyEtiBands(3_000m, StandardRules().Tier1Bands);
        result.Should().Be(1_500m);
    }

    [Fact]
    public void ApplyEtiBands_FixedBand_Tier1_AtMinimumWage_Returns1500()
    {
        var result = EtiCalculationEngine.ApplyEtiBands(2_500m, StandardRules().Tier1Bands);
        result.Should().Be(1_500m);
    }

    [Fact]
    public void ApplyEtiBands_TaperBand_Tier1_R6000_ReturnsCorrect()
    {
        // Tier1, band 3: 5500–7499.99, formula = 1500 - 0.75 × (6000 - 5500) = 1500 - 375 = 1125
        var result = EtiCalculationEngine.ApplyEtiBands(6_000m, StandardRules().Tier1Bands);
        result.Should().Be(1_125m);
    }

    [Fact]
    public void ApplyEtiBands_TaperBand_Tier1_AtFloor_Returns1500()
    {
        // At R5,500 (taper floor): 1500 - 0.75 × (5500 - 5500) = 1500
        var result = EtiCalculationEngine.ApplyEtiBands(5_500m, StandardRules().Tier1Bands);
        result.Should().Be(1_500m);
    }

    [Fact]
    public void ApplyEtiBands_Tier2_FixedBand_Returns750()
    {
        // Tier2, band 2: 2500–5499.99, formula = 750 flat
        var result = EtiCalculationEngine.ApplyEtiBands(3_000m, StandardRules().Tier2Bands);
        result.Should().Be(750m);
    }

    [Fact]
    public void ApplyEtiBands_Tier2_TaperBand_R6000_ReturnsCorrect()
    {
        // Tier2, band 3: 5500–7499.99, formula = 750 - 0.375 × (6000 - 5500) = 750 - 187.5 = 562.5
        var result = EtiCalculationEngine.ApplyEtiBands(6_000m, StandardRules().Tier2Bands);
        result.Should().Be(562.5m);
    }

    [Fact]
    public void ApplyEtiBands_OutsideAllBands_ReturnsZero()
    {
        // R8,000 is above all bands (max is 7499.99) → 0
        var result = EtiCalculationEngine.ApplyEtiBands(8_000m, StandardRules().Tier1Bands);
        result.Should().Be(0m);
    }

    // ── CalculateMonthlyEti — full calculation ─────────────────────────────────

    [Fact]
    public void CalculateMonthlyEti_Tier1_FixedBand_R3000_FullHours_Returns1500()
    {
        var result = EtiCalculationEngine.CalculateMonthlyEti(
            monthlyRemuneration: new MoneyZAR(3_000m),
            tier: EtiTier.Tier1,
            actualHoursWorked: 160,
            rules: StandardRules());
        result.Amount.Should().Be(1_500m);
    }

    [Fact]
    public void CalculateMonthlyEti_Tier2_FixedBand_R3000_FullHours_Returns750()
    {
        var result = EtiCalculationEngine.CalculateMonthlyEti(
            monthlyRemuneration: new MoneyZAR(3_000m),
            tier: EtiTier.Tier2,
            actualHoursWorked: 160,
            rules: StandardRules());
        result.Amount.Should().Be(750m);
    }

    [Fact]
    public void CalculateMonthlyEti_TaperBand_R6000_FullHours_Returns1125()
    {
        // R6,000 → Tier1 taper: 1500 - 0.75×(6000−5500) = R1,125
        var result = EtiCalculationEngine.CalculateMonthlyEti(
            monthlyRemuneration: new MoneyZAR(6_000m),
            tier: EtiTier.Tier1,
            actualHoursWorked: 160,
            rules: StandardRules());
        result.Amount.Should().Be(1_125m);
    }

    [Fact]
    public void CalculateMonthlyEti_HoursProration_GroussUpThenProrate()
    {
        // PRD-16 Section 5 hours proration:
        // R3,000/month, 80 hours worked:
        //   grossed-up remuneration = 3,000 × 160/80 = R6,000
        //   ETI at R6,000 (Tier1 taper) = R1,125
        //   proration factor = 80/160 = 0.5
        //   prorated ETI = 1,125 × 0.5 = R562.50
        var result = EtiCalculationEngine.CalculateMonthlyEti(
            monthlyRemuneration: new MoneyZAR(3_000m),
            tier: EtiTier.Tier1,
            actualHoursWorked: 80,
            rules: StandardRules());
        result.Amount.Should().Be(562.50m);
    }

    [Fact]
    public void CalculateMonthlyEti_IneligibleTier_ReturnsZero()
    {
        var result = EtiCalculationEngine.CalculateMonthlyEti(
            monthlyRemuneration: new MoneyZAR(3_000m),
            tier: EtiTier.Ineligible,
            actualHoursWorked: 160,
            rules: StandardRules());
        result.Amount.Should().Be(0m);
    }

    [Fact]
    public void CalculateMonthlyEti_RemunerationAboveMaximum_ReturnsZero()
    {
        // R7,500 is at the max → not eligible (>= R7,500 check in CalculateMonthlyEti)
        var result = EtiCalculationEngine.CalculateMonthlyEti(
            monthlyRemuneration: new MoneyZAR(7_500m),
            tier: EtiTier.Tier1,
            actualHoursWorked: 160,
            rules: StandardRules());
        result.Amount.Should().Be(0m);
    }

    [Fact]
    public void CalculateMonthlyEti_RemunerationBelowMinWage_ReturnsZero()
    {
        // Below R2,500 minimum wage → not eligible
        var result = EtiCalculationEngine.CalculateMonthlyEti(
            monthlyRemuneration: new MoneyZAR(2_000m),
            tier: EtiTier.Tier1,
            actualHoursWorked: 160,
            rules: StandardRules());
        result.Amount.Should().Be(0m);
    }

    [Fact]
    public void CalculateMonthlyEti_StandardHoursOrMore_NoProration()
    {
        // 200 actual hours ≥ 160 standard → no proration (factor = 1)
        var fullHours = EtiCalculationEngine.CalculateMonthlyEti(
            new MoneyZAR(3_000m), EtiTier.Tier1, actualHoursWorked: 160, rules: StandardRules());
        var moreHours = EtiCalculationEngine.CalculateMonthlyEti(
            new MoneyZAR(3_000m), EtiTier.Tier1, actualHoursWorked: 200, rules: StandardRules());
        moreHours.Amount.Should().Be(fullHours.Amount);
    }

    // ── PRD-16 Section 5: Tier boundary tests ────────────────────────────────

    [Fact]
    public void GetTier_Month12_IsTier1_Month13_IsTier2()
    {
        // Critical boundary: last month of Tier1 vs. first month of Tier2
        var start = new DateOnly(2025, 1, 1);

        var endMonth12 = new DateOnly(2026, 1, 1);  // 12 months → Tier1
        var endMonth13 = new DateOnly(2026, 2, 1);  // 13 months → Tier2

        EtiCalculationEngine.GetTier(start, endMonth12).Should().Be(EtiTier.Tier1);
        EtiCalculationEngine.GetTier(start, endMonth13).Should().Be(EtiTier.Tier2);
    }

    [Fact]
    public void GetTier_Month24_IsTier2_Month25_IsIneligible()
    {
        // Critical boundary: last eligible month vs. first ineligible month
        var start = new DateOnly(2025, 1, 1);

        var endMonth24 = new DateOnly(2027, 1, 1);  // 24 months → Tier2
        var endMonth25 = new DateOnly(2027, 2, 1);  // 25 months → Ineligible

        EtiCalculationEngine.GetTier(start, endMonth24).Should().Be(EtiTier.Tier2);
        EtiCalculationEngine.GetTier(start, endMonth25).Should().Be(EtiTier.Ineligible);
    }

    // ── Guard / null checks ───────────────────────────────────────────────────

    [Fact]
    public void CalculateMonthlyEti_NullRules_ThrowsArgumentNullException()
    {
        var act = () => EtiCalculationEngine.CalculateMonthlyEti(
            new MoneyZAR(3_000m), EtiTier.Tier1, 160, rules: null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsEligible_NullRules_ThrowsArgumentNullException()
    {
        var act = () => EtiCalculationEngine.IsEligible(25, new MoneyZAR(3_000m), EtiTier.Tier1, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── FsCheck property-based tests ─────────────────────────────────────────

    [Property(DisplayName = "Monthly ETI is never negative for any eligible input")]
    public bool CalculateMonthlyEti_IsNeverNegative(PositiveInt payRands)
    {
        // TC-PAY-003 property: ETI ≥ 0 for all eligible remuneration values
        var pay = new MoneyZAR((decimal)payRands.Get);
        var eti = EtiCalculationEngine.CalculateMonthlyEti(pay, EtiTier.Tier1, 160, StandardRules());
        return eti.Amount >= 0m;
    }

    [Property(DisplayName = "Ineligible tier always produces zero ETI regardless of remuneration")]
    public bool CalculateMonthlyEti_IneligibleTier_AlwaysZero(PositiveInt payRands)
    {
        var pay = new MoneyZAR((decimal)payRands.Get);
        var eti = EtiCalculationEngine.CalculateMonthlyEti(pay, EtiTier.Ineligible, 160, StandardRules());
        return eti.Amount == 0m;
    }

    [Property(DisplayName = "Tier2 ETI is always <= Tier1 ETI for the same remuneration")]
    public bool CalculateMonthlyEti_Tier2_NeverExceedsTier1(PositiveInt payRands)
    {
        // TC-PAY-003 property: Second 12 months always have reduced rates
        var pay = new MoneyZAR((decimal)payRands.Get);
        var tier1 = EtiCalculationEngine.CalculateMonthlyEti(pay, EtiTier.Tier1, 160, StandardRules());
        var tier2 = EtiCalculationEngine.CalculateMonthlyEti(pay, EtiTier.Tier2, 160, StandardRules());
        return tier2.Amount <= tier1.Amount;
    }

    [Property(DisplayName = "CalendarMonthsBetween is never negative")]
    public bool CalendarMonthsBetween_IsNeverNegative(NonNegativeInt daysFromNow)
    {
        var start = new DateOnly(2025, 1, 1);
        var end = start.AddDays(daysFromNow.Get);
        return EtiCalculationEngine.CalendarMonthsBetween(start, end) >= 0;
    }
}
