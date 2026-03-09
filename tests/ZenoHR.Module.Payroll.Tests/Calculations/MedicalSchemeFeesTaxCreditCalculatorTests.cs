// TC-PAY-MSFTC: Medical Scheme Fees Tax Credit (MSFTC) calculator tests.
// REQ-HR-003, CTL-SARS-005: Section 6A Income Tax Act — IRP5 code 4116.
// Test values use 2026/2027 rates (primary R364, dependant R364, additional R246)
// sourced from docs/seed-data/sars-medical-scheme-tax-credit-2026-2027.json.
// These values appear in tests only — production code reads them from StatutoryRuleSet.

using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using ZenoHR.Module.Payroll.Calculations;

namespace ZenoHR.Module.Payroll.Tests.Calculations;

/// <summary>
/// Tests for <see cref="MedicalSchemeFeesTaxCreditCalculator"/> covering:
/// - Monthly credit computation for 0–3 dependants (Section 6A formula)
/// - ApplyCredit non-refundable floor-at-zero rule
/// - AnnualiseCredit for monthly (×12) and weekly (×52) pay cycles
/// - FsCheck property: result is never negative
/// </summary>
public sealed class MedicalSchemeFeesTaxCreditCalculatorTests
{
    // 2026/2027 rates from sars-medical-scheme-tax-credit-2026-2027.json
    // Used only in tests — production code loads these from StatutoryRuleSet.
    private const decimal Primary2027    = 364m;
    private const decimal Dependant2027  = 364m;
    private const decimal Additional2027 = 246m;

    // ── CalculateMonthly ─────────────────────────────────────────────────────

    [Fact]
    public void CalculateMonthly_PrimaryOnly_ReturnsPrimaryCredit()
    {
        // TC-PAY-MSFTC-001: 0 dependants → credit = primary only
        var result = MedicalSchemeFeesTaxCreditCalculator.CalculateMonthly(
            dependantCount: 0,
            primaryMonthlyCredit: Primary2027,
            dependantMonthlyCredit: Dependant2027,
            additionalMonthlyCredit: Additional2027);

        result.Should().Be(364m);
    }

    [Fact]
    public void CalculateMonthly_OneDepedant_ReturnsPrimaryPlusDependant()
    {
        // TC-PAY-MSFTC-002: 1 dependant → credit = primary + dependant_1 = 364 + 364 = 728
        var result = MedicalSchemeFeesTaxCreditCalculator.CalculateMonthly(
            dependantCount: 1,
            primaryMonthlyCredit: Primary2027,
            dependantMonthlyCredit: Dependant2027,
            additionalMonthlyCredit: Additional2027);

        result.Should().Be(728m);
    }

    [Fact]
    public void CalculateMonthly_TwoDependants_CorrectTotal()
    {
        // TC-PAY-MSFTC-003: 2 dependants → 364 + 364 + 246×1 = 974
        var result = MedicalSchemeFeesTaxCreditCalculator.CalculateMonthly(
            dependantCount: 2,
            primaryMonthlyCredit: Primary2027,
            dependantMonthlyCredit: Dependant2027,
            additionalMonthlyCredit: Additional2027);

        result.Should().Be(974m);
    }

    [Fact]
    public void CalculateMonthly_ThreeDependants_CorrectTotal()
    {
        // TC-PAY-MSFTC-004: 3 dependants → 364 + 364 + 246×2 = 1220
        var result = MedicalSchemeFeesTaxCreditCalculator.CalculateMonthly(
            dependantCount: 3,
            primaryMonthlyCredit: Primary2027,
            dependantMonthlyCredit: Dependant2027,
            additionalMonthlyCredit: Additional2027);

        result.Should().Be(1_220m);
    }

    [Fact]
    public void CalculateMonthly_NegativeDependants_Throws()
    {
        // TC-PAY-MSFTC-005: negative dependant count is a programming error
        var act = () => MedicalSchemeFeesTaxCreditCalculator.CalculateMonthly(
            dependantCount: -1,
            primaryMonthlyCredit: Primary2027,
            dependantMonthlyCredit: Dependant2027,
            additionalMonthlyCredit: Additional2027);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ── ApplyCredit ──────────────────────────────────────────────────────────

    [Fact]
    public void ApplyCredit_PayeLargerThanCredit_ReducesPaye()
    {
        // TC-PAY-MSFTC-006: PAYE 5000 − credit 364 = 4636
        var result = MedicalSchemeFeesTaxCreditCalculator.ApplyCredit(
            payeLiabilityBeforeMsftc: 5_000m,
            monthlyMsftc: 364m);

        result.Should().Be(4_636m);
    }

    [Fact]
    public void ApplyCredit_CreditLargerThanPaye_ReturnsZero()
    {
        // TC-PAY-MSFTC-007: PAYE 200 − credit 364 → non-refundable, result = 0
        var result = MedicalSchemeFeesTaxCreditCalculator.ApplyCredit(
            payeLiabilityBeforeMsftc: 200m,
            monthlyMsftc: 364m);

        result.Should().Be(0m);
    }

    [Fact]
    public void ApplyCredit_ZeroPaye_ReturnsZero()
    {
        // TC-PAY-MSFTC-008: PAYE already 0 → still 0 (no refund mechanism)
        var result = MedicalSchemeFeesTaxCreditCalculator.ApplyCredit(
            payeLiabilityBeforeMsftc: 0m,
            monthlyMsftc: 364m);

        result.Should().Be(0m);
    }

    // ── AnnualiseCredit ──────────────────────────────────────────────────────

    [Fact]
    public void AnnualiseCredit_Monthly_MultipliesBy12()
    {
        // TC-PAY-MSFTC-009: 728 × 12 = 8736
        var result = MedicalSchemeFeesTaxCreditCalculator.AnnualiseCredit(
            monthlyCredit: 728m,
            periodsPerYear: 12);

        result.Should().Be(8_736m);
    }

    [Fact]
    public void AnnualiseCredit_Weekly_MultipliesBy52()
    {
        // TC-PAY-MSFTC-010: 364 × 52 = 18928
        var result = MedicalSchemeFeesTaxCreditCalculator.AnnualiseCredit(
            monthlyCredit: 364m,
            periodsPerYear: 52);

        result.Should().Be(18_928m);
    }

    // ── FsCheck property tests ───────────────────────────────────────────────

    [Property]
    public Property ApplyCredit_NeverNegative(PositiveInt paye, PositiveInt credit)
        => (MedicalSchemeFeesTaxCreditCalculator.ApplyCredit(paye.Get, credit.Get) >= 0m).ToProperty();
}
