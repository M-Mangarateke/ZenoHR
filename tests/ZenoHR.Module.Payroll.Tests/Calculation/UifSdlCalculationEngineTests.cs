// TC-PAY-002: UIF and SDL calculation engine tests.
// REQ-HR-003, CTL-SARS-002, PRD-16 Sections 6, 8.
// Values verified against sars-uif-sdl.json seed data (R177.12 ceiling, R500k SDL exemption).

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using ZenoHR.Domain.Common;
using ZenoHR.Module.Payroll.Calculation;

namespace ZenoHR.Module.Payroll.Tests.Calculation;

/// <summary>
/// Tests for <see cref="UifSdlCalculationEngine"/> covering monthly and weekly UIF,
/// SDL calculation, and all ceiling and exemption edge cases.
/// </summary>
public sealed class UifSdlCalculationEngineTests
{
    // ── Test fixture ──────────────────────────────────────────────────────────

    private static SarsUifSdlRuleSet StandardRules() => SarsUifSdlRuleSet.CreateForTesting(
        uifEmployeeRate: 0.01m,
        uifEmployerRate: 0.01m,
        uifMonthlyCeiling: 17_712.00m,
        maxEmployeeMonthly: 177.12m,
        maxEmployerMonthly: 177.12m,
        sdlRate: 0.01m,
        sdlExemptionThresholdAnnual: 500_000.00m);

    // ── Monthly UIF — employee ─────────────────────────────────────────────

    [Theory]
    [InlineData(5_000,   50.00)]    // R5,000 × 0.01 = R50.00 (well below ceiling)
    [InlineData(10_000, 100.00)]    // R10,000 × 0.01 = R100.00 (below ceiling)
    [InlineData(17_712, 177.12)]    // at the R17,712 ceiling → exactly R177.12
    [InlineData(20_000, 177.12)]    // R20,000 × 0.01 = R200 → capped at R177.12
    [InlineData(50_000, 177.12)]    // high salary → always capped
    [InlineData(100_000, 177.12)]   // very high salary → always capped
    public void CalculateUifEmployee_Monthly_CeilingApplied(double grossPay, double expected)
    {
        var result = UifSdlCalculationEngine.CalculateUifEmployee(
            new MoneyZAR((decimal)grossPay), StandardRules());
        result.Amount.Should().Be((decimal)expected);
    }

    [Fact]
    public void CalculateUifEmployee_ZeroPay_ReturnsZero()
    {
        var result = UifSdlCalculationEngine.CalculateUifEmployee(MoneyZAR.Zero, StandardRules());
        result.Amount.Should().Be(0m);
    }

    // ── Monthly UIF — employer ────────────────────────────────────────────────

    [Theory]
    [InlineData(5_000,   50.00)]
    [InlineData(17_712, 177.12)]
    [InlineData(20_000, 177.12)]
    public void CalculateUifEmployer_Monthly_MatchesEmployeeContribution(double grossPay, double expected)
    {
        // UIF: employee and employer contribute the same rate (both 1%, same ceiling)
        var result = UifSdlCalculationEngine.CalculateUifEmployer(
            new MoneyZAR((decimal)grossPay), StandardRules());
        result.Amount.Should().Be((decimal)expected);
    }

    // ── Weekly UIF ────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateUifEmployeeWeekly_BelowWeeklyCeiling_Returns1Percent()
    {
        // Weekly ceiling = 17,712 × 12 / 52 = 4,087.384...
        // R2,000/week × 0.01 = R20.00 (below ceiling)
        var result = UifSdlCalculationEngine.CalculateUifEmployeeWeekly(
            new MoneyZAR(2_000m), StandardRules());
        result.Amount.Should().Be(20.00m);
    }

    [Fact]
    public void CalculateUifEmployeeWeekly_AboveWeeklyCeiling_IsCapped()
    {
        // Weekly ceiling = 17,712 × 12 / 52 ≈ 4,087.38
        // R10,000/week → 10,000 × 0.01 = R100 but ceiling is capped at ceiling × rate
        // cappedPay = min(10000, 4087.38...) = 4087.38...; contribution = 4087.38 × 0.01 = 40.87...
        var weeklyCeiling = 17_712.00m * 12m / 52m;   // ≈ 4,087.384615...
        var expectedUif = Math.Round(weeklyCeiling * 0.01m, 2, MidpointRounding.AwayFromZero);

        var result = UifSdlCalculationEngine.CalculateUifEmployeeWeekly(
            new MoneyZAR(10_000m), StandardRules());
        result.Amount.Should().Be(expectedUif);
    }

    [Fact]
    public void CalculateUifEmployeeWeekly_AtWeeklyCeiling_ReturnsExactMax()
    {
        // Exactly at the weekly ceiling: rate applied without capping
        var weeklyCeiling = new MoneyZAR(17_712.00m * 12m / 52m);
        var expectedUif = (weeklyCeiling * 0.01m).RoundToCent();
        var result = UifSdlCalculationEngine.CalculateUifEmployeeWeekly(weeklyCeiling, StandardRules());
        result.Amount.Should().Be(expectedUif.Amount);
    }

    // ── SDL ───────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(10_000, false, 100.00)]   // R10,000 × 0.01 = R100 (not exempt)
    [InlineData(30_000, false, 300.00)]   // R30,000 × 0.01 = R300
    [InlineData(30_000, true,  0.00)]     // exempt → R0 regardless of salary
    [InlineData(100_000, true, 0.00)]     // very high salary + exempt → always R0
    public void CalculateSdl_ExemptionFlag_ControlsWhetherSdlIsDue(
        double grossPay, bool exempt, double expected)
    {
        var result = UifSdlCalculationEngine.CalculateSdl(
            new MoneyZAR((decimal)grossPay), StandardRules(), isEmployerSdlExempt: exempt);
        result.Amount.Should().Be((decimal)expected);
    }

    [Fact]
    public void CalculateSdl_ZeroPay_NonExempt_ReturnsZero()
    {
        // R0 × 0.01 = R0 (employer-only, no deduction from employee)
        var result = UifSdlCalculationEngine.CalculateSdl(
            MoneyZAR.Zero, StandardRules(), isEmployerSdlExempt: false);
        result.Amount.Should().Be(0m);
    }

    // ── PRD-16 Section 6: PAYE floor / UIF interaction ──────────────────────

    [Fact]
    public void CalculateUifEmployee_WhenPayeIsZero_UifStillApplies()
    {
        // PRD-16 Section 6: UIF is calculated independently of PAYE.
        // Even when income is below PAYE threshold, UIF still applies.
        // R7,000/month (below PAYE threshold): UIF = 7,000 × 0.01 = R70.00
        var result = UifSdlCalculationEngine.CalculateUifEmployee(
            new MoneyZAR(7_000m), StandardRules());
        result.Amount.Should().Be(70.00m);
    }

    // ── Guard / null checks ───────────────────────────────────────────────────

    [Fact]
    public void CalculateUifEmployee_NullRules_ThrowsArgumentNullException()
    {
        var act = () => UifSdlCalculationEngine.CalculateUifEmployee(
            new MoneyZAR(10_000m), rules: null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CalculateSdl_NullRules_ThrowsArgumentNullException()
    {
        var act = () => UifSdlCalculationEngine.CalculateSdl(
            new MoneyZAR(10_000m), rules: null!, isEmployerSdlExempt: false);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── FsCheck property-based tests ─────────────────────────────────────────

    [Property(DisplayName = "Employee UIF never exceeds R177.12 for any gross pay")]
    public bool CalculateUifEmployee_NeverExceedsCeiling(NonNegativeInt payRands)
    {
        // TC-PAY-002 property: UIF ≤ MaxEmployeeMonthly for all inputs
        var pay = new MoneyZAR((decimal)payRands.Get);
        var uif = UifSdlCalculationEngine.CalculateUifEmployee(pay, StandardRules());
        return uif.Amount <= 177.12m;
    }

    [Property(DisplayName = "Employee UIF is never negative")]
    public bool CalculateUifEmployee_IsNeverNegative(NonNegativeInt payRands)
    {
        var pay = new MoneyZAR((decimal)payRands.Get);
        var uif = UifSdlCalculationEngine.CalculateUifEmployee(pay, StandardRules());
        return uif.Amount >= 0m;
    }

    [Property(DisplayName = "SDL is never negative for non-exempt employer")]
    public bool CalculateSdl_NonExempt_IsNeverNegative(NonNegativeInt payRands)
    {
        var pay = new MoneyZAR((decimal)payRands.Get);
        var sdl = UifSdlCalculationEngine.CalculateSdl(pay, StandardRules(), isEmployerSdlExempt: false);
        return sdl.Amount >= 0m;
    }

    [Property(DisplayName = "SDL is always zero when employer is SDL-exempt")]
    public bool CalculateSdl_ExemptEmployer_AlwaysZero(NonNegativeInt payRands)
    {
        var pay = new MoneyZAR((decimal)payRands.Get);
        var sdl = UifSdlCalculationEngine.CalculateSdl(pay, StandardRules(), isEmployerSdlExempt: true);
        return sdl.Amount == 0m;
    }

    [Property(DisplayName = "Employee and employer UIF contributions are always equal")]
    public bool CalculateUif_EmployeeAndEmployerContributionsAreEqual(NonNegativeInt payRands)
    {
        // Both sides use the same 1% rate and same ceiling — must produce identical amounts
        var pay = new MoneyZAR((decimal)payRands.Get);
        var employee = UifSdlCalculationEngine.CalculateUifEmployee(pay, StandardRules());
        var employer = UifSdlCalculationEngine.CalculateUifEmployer(pay, StandardRules());
        return employee.Amount == employer.Amount;
    }
}
