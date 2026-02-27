// TC-PAY-001: PAYE calculation engine tests — deterministic + FsCheck property-based.
// REQ-HR-003, CTL-SARS-001, PRD-16 Sections 1–3 and Appendix B.
// All 2025/2026 tax values verified against SARS-published brackets, rebates, and thresholds.

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using ZenoHR.Domain.Common;
using ZenoHR.Module.Payroll.Calculation;

namespace ZenoHR.Module.Payroll.Tests.Calculation;

/// <summary>
/// Tests for <see cref="PayeCalculationEngine"/> covering all PRD-16 Section 1–3 scenarios.
/// Rule set is constructed from the 2025/2026 seed data values via CreateForTesting().
/// </summary>
public sealed class PayeCalculationEngineTests
{
    // ── Test fixture ──────────────────────────────────────────────────────────

    /// <summary>
    /// Standard 2025/2026 SARS PAYE rule set from sars-paye-2025-2026.json.
    /// Used in all deterministic tests. CTL-SARS-001.
    /// </summary>
    private static SarsPayeRuleSet Rules2026() => SarsPayeRuleSet.CreateForTesting(
        brackets: new List<PayeTaxBracket>
        {
            new() { Min = 1m,        Max = 237_100m,    Rate = 0.18m, BaseTax = 0m },
            new() { Min = 237_101m,  Max = 370_500m,    Rate = 0.26m, BaseTax = 42_678m },
            new() { Min = 370_501m,  Max = 512_800m,    Rate = 0.31m, BaseTax = 77_362m },
            new() { Min = 512_801m,  Max = 673_000m,    Rate = 0.36m, BaseTax = 121_475m },
            new() { Min = 673_001m,  Max = 857_900m,    Rate = 0.39m, BaseTax = 179_147m },
            new() { Min = 857_901m,  Max = 1_817_000m,  Rate = 0.41m, BaseTax = 251_258m },
            new() { Min = 1_817_001m, Max = null,       Rate = 0.45m, BaseTax = 644_489m },
        },
        primary: 17_235m, secondary: 9_444m, tertiary: 3_145m,
        thresholdBelow65: 95_750m, thresholdAge65To74: 148_217m, thresholdAge75Plus: 165_689m,
        taxYear: "2026");

    // ── PRD-16 Section 1: Monthly PAYE deterministic ──────────────────────────

    [Fact]
    public void CalculateMonthlyPAYE_IncomeBelowThreshold_ReturnsZero()
    {
        // Age 32, R7,000/month → annual R84,000 → gross tax R15,120 → rebate R17,235 → floor → R0
        var result = PayeCalculationEngine.CalculateMonthlyPAYE(
            new MoneyZAR(7_000m), age: 32, ruleSet: Rules2026());
        result.Amount.Should().Be(0m);
    }

    [Fact]
    public void CalculateMonthlyPAYE_ZeroIncome_ReturnsZero()
    {
        var result = PayeCalculationEngine.CalculateMonthlyPAYE(
            MoneyZAR.Zero, age: 32, ruleSet: Rules2026());
        result.Amount.Should().Be(0m);
    }

    [Fact]
    public void CalculateMonthlyPAYE_AtExactAnnualThreshold_ReturnsZero()
    {
        // Threshold = R95,750/year = R7,979.166.../month. Use R7,979/month → annual R95,748 < threshold
        // At threshold: annual = R95,750 → gross tax = 0.18 × 95,750 = R17,235 = rebate → floor → R0
        var thresholdMonthly = new MoneyZAR(95_750m / 12m); // exact monthly threshold
        var result = PayeCalculationEngine.CalculateMonthlyPAYE(
            thresholdMonthly, age: 32, ruleSet: Rules2026());
        result.Amount.Should().Be(0m);
    }

    [Fact]
    public void CalculateMonthlyPAYE_JustAboveThreshold_ReturnsSmallPositiveAmount()
    {
        // R8,000/month → annual R96,000
        // gross = 0.18 × 96,000 = R17,280; rebate = R17,235; annual = R45; monthly = R3.75
        var result = PayeCalculationEngine.CalculateMonthlyPAYE(
            new MoneyZAR(8_000m), age: 32, ruleSet: Rules2026());
        result.Amount.Should().Be(3.75m);
    }

    [Fact]
    public void CalculateMonthlyPAYE_MiddleSalary_MatchesBracket2()
    {
        // R20,000/month → annual R240,000 (Bracket 2: R237,101–R370,500, 26%, base R42,678)
        // excess = 240,000 − 237,100 = 2,900
        // gross = 42,678 + 0.26 × 2,900 = 43,432; rebate 17,235; annual = 26,197
        // monthly = 26,197 / 12 = 2,183.0833... → R2,183.08
        var result = PayeCalculationEngine.CalculateMonthlyPAYE(
            new MoneyZAR(20_000m), age: 32, ruleSet: Rules2026());
        result.Amount.Should().Be(2_183.08m);
    }

    [Fact]
    public void CalculateMonthlyPAYE_HighSalary_MatchesBracket4()
    {
        // R50,000/month → annual R600,000 (Bracket 4: R512,801–R673,000, 36%, base R121,475)
        // excess = 600,000 − 512,800 = 87,200
        // gross = 121,475 + 0.36 × 87,200 = 152,867; rebate 17,235; annual = 135,632
        // monthly = 135,632 / 12 = 11,302.666... → R11,302.67
        var result = PayeCalculationEngine.CalculateMonthlyPAYE(
            new MoneyZAR(50_000m), age: 32, ruleSet: Rules2026());
        result.Amount.Should().Be(11_302.67m);
    }

    [Fact]
    public void CalculateMonthlyPAYE_TopBracket_MatchesBracket7()
    {
        // R200,000/month → annual R2,400,000 (Bracket 7: R1,817,001+, 45%, base R644,489)
        // excess = 2,400,000 − (1,817,001 − 1) = 2,400,000 − 1,817,000 = 583,000
        // gross = 644,489 + 0.45 × 583,000 = 644,489 + 262,350 = 906,839
        // rebate 17,235; annual = 889,604; monthly = 889,604 / 12 = 74,133.666... → R74,133.67
        var result = PayeCalculationEngine.CalculateMonthlyPAYE(
            new MoneyZAR(200_000m), age: 32, ruleSet: Rules2026());
        result.Amount.Should().Be(74_133.67m);
    }

    // ── Age-based rebate tests ───────────────────────────────────────────────

    [Fact]
    public void CalculateMonthlyPAYE_Age65AtThreshold_ReturnsZero()
    {
        // Age 65–74 threshold = R148,217/year = R12,351.42.../month
        // At exact threshold: gross = 0.18 × 148,217 = R26,679; rebate = 17,235 + 9,444 = 26,679 → R0
        var thresholdMonthly = new MoneyZAR(148_217m / 12m);
        var result = PayeCalculationEngine.CalculateMonthlyPAYE(
            thresholdMonthly, age: 65, ruleSet: Rules2026());
        result.Amount.Should().Be(0m);
    }

    [Fact]
    public void CalculateMonthlyPAYE_Age65AboveThreshold_UsesSecondaryRebate()
    {
        // R15,000/month → annual R180,000
        // gross = 0.18 × 180,000 = R32,400; rebate = 17,235 + 9,444 = 26,679
        // annual = R5,721; monthly = 5,721 / 12 = R476.75
        var result = PayeCalculationEngine.CalculateMonthlyPAYE(
            new MoneyZAR(15_000m), age: 65, ruleSet: Rules2026());
        result.Amount.Should().Be(476.75m);
    }

    [Fact]
    public void CalculateMonthlyPAYE_Age75_UsesTertiaryRebate()
    {
        // R15,000/month → annual R180,000
        // gross = 0.18 × 180,000 = R32,400; rebate = 17,235 + 9,444 + 3,145 = 29,824
        // annual = R2,576; monthly = 2,576 / 12 = 214.666... → R214.67
        var result = PayeCalculationEngine.CalculateMonthlyPAYE(
            new MoneyZAR(15_000m), age: 75, ruleSet: Rules2026());
        result.Amount.Should().Be(214.67m);
    }

    [Fact]
    public void CalculateMonthlyPAYE_Age74VsAge75_TertiaryRebateReducesPAYE()
    {
        // Age 74 and 75 at same salary: age 75 should pay less (extra tertiary rebate)
        var age74 = PayeCalculationEngine.CalculateMonthlyPAYE(
            new MoneyZAR(20_000m), age: 74, ruleSet: Rules2026());
        var age75 = PayeCalculationEngine.CalculateMonthlyPAYE(
            new MoneyZAR(20_000m), age: 75, ruleSet: Rules2026());
        age75.Amount.Should().BeLessThan(age74.Amount);
    }

    // ── PRD-16 Section 2: Mid-period joiner ──────────────────────────────────

    [Fact]
    public void CalculateJoinerPAYE_FullMonth_EqualsMonthlyPAYE()
    {
        // 31 days worked out of 31 → full month → same as monthly PAYE
        var joiner = PayeCalculationEngine.CalculateJoinerPAYE(
            new MoneyZAR(50_000m), daysWorked: 31, daysInMonth: 31, age: 32, ruleSet: Rules2026());
        var monthly = PayeCalculationEngine.CalculateMonthlyPAYE(
            new MoneyZAR(50_000m), age: 32, ruleSet: Rules2026());
        joiner.Amount.Should().Be(monthly.Amount);
    }

    [Fact]
    public void CalculateJoinerPAYE_PartialMonth_ProRatesOnPAYENotOnSalary()
    {
        // R50,000/month, 17 out of 31 days — PRD-16 Section 2 key rule:
        // PAYE annualisation uses full R50,000 package (not pro-rated salary).
        // Then only the monthly PAYE is pro-rated.
        // Full monthly PAYE = R11,302.67 (from CalculateMonthlyPAYE test above)
        // Pro-rated = 11,302.67 × 17/31 = 11,302.67 × 0.5483... = 6,198.238... → R6,198.24
        var result = PayeCalculationEngine.CalculateJoinerPAYE(
            new MoneyZAR(50_000m), daysWorked: 17, daysInMonth: 31, age: 32, ruleSet: Rules2026());
        result.Amount.Should().Be(6_198.24m);
    }

    [Fact]
    public void CalculateJoinerPAYE_ZeroDaysWorked_ReturnsZero()
    {
        var result = PayeCalculationEngine.CalculateJoinerPAYE(
            new MoneyZAR(50_000m), daysWorked: 0, daysInMonth: 31, age: 32, ruleSet: Rules2026());
        result.Amount.Should().Be(0m);
    }

    // ── PRD-16 Section 3: Weekly PAYE ─────────────────────────────────────────

    [Fact]
    public void CalculateWeeklyPAYE_KnownWeeklySalary_MatchesBracket4()
    {
        // R12,000/week → annual R624,000 (Bracket 4: R512,801–R673,000)
        // excess = 624,000 − 512,800 = 111,200
        // gross = 121,475 + 0.36 × 111,200 = 161,507; rebate 17,235; annual = 144,272
        // weekly = 144,272 / 52 = 2,774.461... → R2,774.46
        var result = PayeCalculationEngine.CalculateWeeklyPAYE(
            new MoneyZAR(12_000m), age: 32, ruleSet: Rules2026());
        result.Amount.Should().Be(2_774.46m);
    }

    [Fact]
    public void CalculateWeeklyPAYE_BelowAnnualThreshold_ReturnsZero()
    {
        // Weekly = R1,800 → annual = R93,600 < R95,750 threshold → zero PAYE
        var result = PayeCalculationEngine.CalculateWeeklyPAYE(
            new MoneyZAR(1_800m), age: 32, ruleSet: Rules2026());
        result.Amount.Should().Be(0m);
    }

    // ── GetAnnualThreshold ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0,  95_750)]
    [InlineData(32, 95_750)]
    [InlineData(64, 95_750)]
    [InlineData(65, 148_217)]
    [InlineData(74, 148_217)]
    [InlineData(75, 165_689)]
    [InlineData(99, 165_689)]
    public void GetAnnualThreshold_ReturnsCorrectThresholdForAge(int age, decimal expected)
    {
        var result = PayeCalculationEngine.GetAnnualThreshold(age, Rules2026());
        result.Amount.Should().Be(expected);
    }

    // ── Internal helpers: ApplyBrackets, GetRebate, CalculateAnnualTax ────────

    [Fact]
    public void ApplyBrackets_ZeroIncome_ReturnsZero()
    {
        var result = PayeCalculationEngine.ApplyBrackets(MoneyZAR.Zero, Rules2026().Brackets);
        result.Amount.Should().Be(0m);
    }

    [Fact]
    public void ApplyBrackets_IncomeInBracket1_AppliesFlat18Percent()
    {
        // R100,000 → bracket 1 → base=0 + 0.18 × (100,000 − 0) = R18,000
        var result = PayeCalculationEngine.ApplyBrackets(new MoneyZAR(100_000m), Rules2026().Brackets);
        result.Amount.Should().Be(18_000m);
    }

    [Fact]
    public void ApplyBrackets_IncomeAtBracketBoundary_MatchesSeedBaseValue()
    {
        // Exact start of bracket 2: R237,101 → base=42,678 + 0.26 × (237,101 − 237,100) = 42,678 + 0.26
        var result = PayeCalculationEngine.ApplyBrackets(new MoneyZAR(237_101m), Rules2026().Brackets);
        result.Amount.Should().Be(42_678m + 0.26m);
    }

    [Fact]
    public void GetRebate_Age32_ReturnsPrimaryOnly()
    {
        var rebate = PayeCalculationEngine.GetRebate(32, Rules2026());
        rebate.Amount.Should().Be(17_235m);
    }

    [Fact]
    public void GetRebate_Age65_ReturnsPrimaryPlusSecondary()
    {
        var rebate = PayeCalculationEngine.GetRebate(65, Rules2026());
        rebate.Amount.Should().Be(17_235m + 9_444m);
    }

    [Fact]
    public void GetRebate_Age75_ReturnsAllThreeRebates()
    {
        var rebate = PayeCalculationEngine.GetRebate(75, Rules2026());
        rebate.Amount.Should().Be(17_235m + 9_444m + 3_145m);
    }

    [Fact]
    public void CalculateAnnualTax_IncomeBelowRebate_FloorsAtZero()
    {
        // Annual R50,000 → gross = 0.18 × 50,000 = R9,000; rebate R17,235 → negative → floor R0
        var result = PayeCalculationEngine.CalculateAnnualTax(
            new MoneyZAR(50_000m), age: 32, ruleSet: Rules2026());
        result.Amount.Should().Be(0m);
    }

    [Fact]
    public void CalculateAnnualTax_HighIncome_RoundsToNearestRand()
    {
        // Annual R600,000 → annual = R135,632 (already whole) — tests round-to-rand path
        var result = PayeCalculationEngine.CalculateAnnualTax(
            new MoneyZAR(600_000m), age: 32, ruleSet: Rules2026());
        result.Amount.Should().Be(135_632m);
    }

    // ── PRD-16 Appendix B: FsCheck property-based tests ──────────────────────

    [Property(DisplayName = "Monthly PAYE is never negative for any non-negative income (age 32)")]
    public bool CalculateMonthlyPAYE_AnyNonNegativeIncome_IsNonNegative(NonNegativeInt incomeRands)
    {
        // TC-PAY-001 property 1
        var income = new MoneyZAR((decimal)incomeRands.Get);
        var paye = PayeCalculationEngine.CalculateMonthlyPAYE(income, age: 32, ruleSet: Rules2026());
        return paye.Amount >= 0m;
    }

    [Property(DisplayName = "Monthly PAYE never exceeds gross income")]
    public bool CalculateMonthlyPAYE_NeverExceedsGrossIncome(NonNegativeInt incomeRands)
    {
        // TC-PAY-001 property 3
        var income = new MoneyZAR((decimal)incomeRands.Get);
        var paye = PayeCalculationEngine.CalculateMonthlyPAYE(income, age: 32, ruleSet: Rules2026());
        return paye.Amount <= income.Amount;
    }

    [Property(DisplayName = "Monthly PAYE increases monotonically with income (age 32)")]
    public bool CalculateMonthlyPAYE_IsMonotonicallyIncreasing(PositiveInt lowerRands, PositiveInt addRands)
    {
        // TC-PAY-001 property: more income → same or more PAYE
        var lower = new MoneyZAR((decimal)lowerRands.Get);
        var higher = new MoneyZAR((decimal)(lowerRands.Get + addRands.Get));
        var lowerPaye  = PayeCalculationEngine.CalculateMonthlyPAYE(lower, age: 32, ruleSet: Rules2026());
        var higherPaye = PayeCalculationEngine.CalculateMonthlyPAYE(higher, age: 32, ruleSet: Rules2026());
        return higherPaye.Amount >= lowerPaye.Amount;
    }

    [Property(DisplayName = "Income at or below annual threshold / 12 produces zero PAYE (age 32)")]
    public bool CalculateMonthlyPAYE_BelowMonthlyThreshold_IsZero(PositiveInt fraction)
    {
        // TC-PAY-001 property 2: income ≤ threshold/12 → PAYE = 0
        // Use 1..100 rands below the threshold to guarantee always-below
        var belowThreshold = new MoneyZAR(95_750m / 12m - (decimal)(fraction.Get % 100));
        if (belowThreshold.IsZeroOrNegative) return true; // skip degenerate cases
        var paye = PayeCalculationEngine.CalculateMonthlyPAYE(belowThreshold, age: 32, ruleSet: Rules2026());
        return paye.Amount == 0m;
    }

    [Property(DisplayName = "Weekly PAYE is never negative for any non-negative income")]
    public bool CalculateWeeklyPAYE_AnyNonNegativeIncome_IsNonNegative(NonNegativeInt incomeRands)
    {
        var income = new MoneyZAR((decimal)incomeRands.Get);
        var paye = PayeCalculationEngine.CalculateWeeklyPAYE(income, age: 32, ruleSet: Rules2026());
        return paye.Amount >= 0m;
    }

    // ── Guard/argument validation ────────────────────────────────────────────

    [Fact]
    public void CalculateMonthlyPAYE_NullRuleSet_ThrowsArgumentNullException()
    {
        var act = () => PayeCalculationEngine.CalculateMonthlyPAYE(
            new MoneyZAR(10_000m), age: 32, ruleSet: null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CalculateMonthlyPAYE_NegativeAge_ThrowsArgumentOutOfRangeException()
    {
        var act = () => PayeCalculationEngine.CalculateMonthlyPAYE(
            new MoneyZAR(10_000m), age: -1, ruleSet: Rules2026());
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CalculateJoinerPAYE_DaysWorkedGreaterThanDaysInMonth_ThrowsArgumentOutOfRangeException()
    {
        var act = () => PayeCalculationEngine.CalculateJoinerPAYE(
            new MoneyZAR(50_000m), daysWorked: 32, daysInMonth: 31, age: 32, ruleSet: Rules2026());
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
