// TC-COMP-010: TaxYearImportService unit tests — pure regression logic and JSON validation.
// CTL-SARS-001, REQ-COMP-015
// TASK-138: Annual SARS tax year import + regression + activation workflow.
//
// Strategy: tests target the pure static helpers on TaxYearImportService
// (ValidatePayeJson, ParsePayeJson, BuildRegressionSamples) and the
// RegressionSample value object — no Firestore connection required.

using FluentAssertions;
using ZenoHR.Domain.Errors;
using ZenoHR.Infrastructure.Services.TaxYear;
using ZenoHR.Module.Payroll.Calculation;

namespace ZenoHR.Domain.Tests.TaxYearImport;

/// <summary>
/// Unit tests for TaxYearImportService pure static helpers and value objects.
/// No Firestore access — all tests are deterministic and fast.
/// CTL-SARS-001, REQ-COMP-015
/// </summary>
public sealed class TaxYearImportServiceTests
{
    // ── Shared fixtures ───────────────────────────────────────────────────────

    /// <summary>
    /// Valid PAYE JSON matching the seed-data format (2025/2026 brackets).
    /// CTL-SARS-001: Values taken from sars-paye-2025-2026.json — not hardcoded in production.
    /// </summary>
    private static string ValidPayeJson(string taxYear = "2026") => $$"""
        {
          "rule_domain": "SARS_PAYE",
          "version": "{{taxYear}}.1.0",
          "effective_from": "{{int.Parse(taxYear) - 1}}-03-01",
          "tax_year": "{{taxYear}}",
          "tax_brackets": [
            { "min": 1, "max": 237100, "rate": 0.18, "base_tax": 0 },
            { "min": 237101, "max": 370500, "rate": 0.26, "base_tax": 42678 },
            { "min": 370501, "max": 512800, "rate": 0.31, "base_tax": 77362 },
            { "min": 512801, "max": 673000, "rate": 0.36, "base_tax": 121475 },
            { "min": 673001, "max": 857900, "rate": 0.39, "base_tax": 179147 },
            { "min": 857901, "max": 1817000, "rate": 0.41, "base_tax": 251258 },
            { "min": 1817001, "max": null, "rate": 0.45, "base_tax": 644489 }
          ],
          "rebates": {
            "primary": 17235,
            "secondary_age_65_plus": 9444,
            "tertiary_age_75_plus": 3145
          },
          "tax_thresholds": {
            "below_age_65": 95750,
            "age_65_to_74": 148217,
            "age_75_and_over": 165689
          }
        }
        """;

    /// <summary>
    /// Creates a SarsPayeRuleSet from the test fixture JSON (via ParsePayeJson).
    /// Used for BuildRegressionSamples tests.
    /// </summary>
    private static SarsPayeRuleSet RuleSetFrom(string json)
    {
        var result = TaxYearImportService.ParsePayeJson(json);
        result.IsSuccess.Should().BeTrue("fixture JSON must be valid");
        return result.Value;
    }

    // ── 1. ValidatePayeJson — valid JSON ──────────────────────────────────────

    [Fact]
    public void ValidatePayeJson_ValidJson_ReturnsSuccess()
    {
        // CTL-SARS-001: Well-formed PAYE JSON with 7 brackets must pass validation.
        var result = TaxYearImportService.ValidatePayeJson(ValidPayeJson());
        result.IsSuccess.Should().BeTrue();
    }

    // ── 2. ValidatePayeJson — missing tax_brackets ────────────────────────────

    [Fact]
    public void ValidatePayeJson_MissingTaxBrackets_ReturnsFailure()
    {
        // REQ-COMP-015: JSON without tax_brackets is structurally invalid.
        const string json = """
            {
              "tax_year": "2027",
              "rebates": { "primary": 17235, "secondary_age_65_plus": 9444, "tertiary_age_75_plus": 3145 }
            }
            """;

        var result = TaxYearImportService.ValidatePayeJson(json);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Contain("tax_brackets");
    }

    // ── 3. ValidatePayeJson — too few brackets ────────────────────────────────

    [Fact]
    public void ValidatePayeJson_TooFewBrackets_ReturnsFailure()
    {
        // REQ-COMP-015: Fewer than 3 tax brackets indicates malformed data.
        const string json = """
            {
              "tax_year": "2027",
              "tax_brackets": [
                { "min": 1, "max": 237100, "rate": 0.18, "base_tax": 0 },
                { "min": 237101, "max": null, "rate": 0.26, "base_tax": 42678 }
              ],
              "rebates": { "primary": 17235, "secondary_age_65_plus": 9444, "tertiary_age_75_plus": 3145 },
              "tax_thresholds": { "below_age_65": 95750, "age_65_to_74": 148217, "age_75_and_over": 165689 }
            }
            """;

        var result = TaxYearImportService.ValidatePayeJson(json);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Contain("minimum 3");
    }

    // ── 4. ValidatePayeJson — too many brackets ───────────────────────────────

    [Fact]
    public void ValidatePayeJson_TooManyBrackets_ReturnsFailure()
    {
        // REQ-COMP-015: More than 10 tax brackets is out of spec.
        var brackets = string.Join(",\n", Enumerable.Range(1, 11).Select(i =>
            $"{{ \"min\": {i * 100000 + 1}, \"max\": {(i + 1) * 100000}, \"rate\": 0.18, \"base_tax\": 0 }}"));
        var json = $$"""
            {
              "tax_year": "2027",
              "tax_brackets": [{{brackets}}],
              "rebates": { "primary": 17235, "secondary_age_65_plus": 9444, "tertiary_age_75_plus": 3145 },
              "tax_thresholds": { "below_age_65": 95750, "age_65_to_74": 148217, "age_75_and_over": 165689 }
            }
            """;

        var result = TaxYearImportService.ValidatePayeJson(json);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Contain("maximum 10");
    }

    // ── 5. BuildRegressionSamples — identical rule sets produce zero differences ──

    [Fact]
    public void BuildRegressionSamples_SameRuleSet_ReturnsPassedWithZeroDifferences()
    {
        // CTL-SARS-001, REQ-COMP-015: When old == new (no rate change), all PAYE deltas must be zero.
        var ruleSet = RuleSetFrom(ValidPayeJson("2026"));

        var (samples, passed, warnings, errors) =
            TaxYearImportService.BuildRegressionSamples(ruleSet, ruleSet);

        passed.Should().BeTrue();
        warnings.Should().BeEmpty();
        errors.Should().BeEmpty();
        samples.Should().NotBeEmpty();
        samples.Should().AllSatisfy(s =>
            s.PayeDifference.Should().Be(0m,
                $"sample {s.EmployeeId} has the same rule set — PAYE must not change"));
    }

    // ── 6. BuildRegressionSamples — material change produces errors (>R1000, <R2000) ────

    [Fact]
    public void BuildRegressionSamples_MaterialChange_ReturnsErrors()
    {
        // REQ-COMP-015: Primary rebate reduction of R1,235 causes annual PAYE delta of R1,235
        // for earners above threshold — this exceeds the error threshold (R1,000) but NOT the
        // fail threshold (R2,000), so regression passes with error messages.
        var oldRuleSet = RuleSetFrom(ValidPayeJson("2026"));

        // Construct a new rule set with a reduced primary rebate (R17235 → R16000).
        // Reduction of R1,235: causes annual PAYE increase of exactly R1,235 for all
        // earners above the tax threshold.
        var newRuleSet = SarsPayeRuleSet.CreateForTesting(
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
            primary: 16_000m,    // reduced by R1,235 — PAYE increase > R1,000 per earner
            secondary: 9_444m, tertiary: 3_145m,
            thresholdBelow65: 95_750m, thresholdAge65To74: 148_217m, thresholdAge75Plus: 165_689m,
            taxYear: "2027");

        var (samples, passed, warnings, errors) =
            TaxYearImportService.BuildRegressionSamples(oldRuleSet, newRuleSet);

        // R1,235 diff > error threshold (R1,000) — produces error-level messages.
        errors.Should().NotBeEmpty(
            "primary rebate reduction of R1,235 exceeds the R1,000 error threshold");
        // R1,235 < fail threshold (R2,000) — regression still passes.
        passed.Should().BeTrue("all differences are below the R2,000 fail threshold");
        _ = samples; // samples are computed correctly
        _ = warnings; // warnings may be empty (errors absorb the messages at higher severity)
    }

    // ── 7. BuildRegressionSamples — massive change fails regression ───────────

    [Fact]
    public void BuildRegressionSamples_MassiveChange_ReturnsFailed()
    {
        // REQ-COMP-015: A change of > R2,000 annual PAYE for any sample must fail regression.
        var oldRuleSet = RuleSetFrom(ValidPayeJson("2026"));

        // Drastically reduce primary rebate to near-zero to cause a massive PAYE increase.
        var newRuleSet = SarsPayeRuleSet.CreateForTesting(
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
            primary: 1_000m,   // massively reduced — PAYE increase >> R2000 for earners > threshold
            secondary: 9_444m, tertiary: 3_145m,
            thresholdBelow65: 95_750m, thresholdAge65To74: 148_217m, thresholdAge75Plus: 165_689m,
            taxYear: "2027");

        var (_, passed, _, errors) =
            TaxYearImportService.BuildRegressionSamples(oldRuleSet, newRuleSet);

        passed.Should().BeFalse("a rebate reduction of >R16,000 must fail regression");
        errors.Should().NotBeEmpty("fail-level differences must produce error messages");
    }

    // ── 8. RegressionSample — PayeDifference and IsMaterialChange computed correctly ──

    [Fact]
    public void RegressionSample_PayeDifferenceAndIsMaterialChange_ComputedCorrectly()
    {
        // CTL-SARS-001: Value object computed properties must be correct.
        var sample = new RegressionSample
        {
            EmployeeId = "Sample_R360000",
            AnnualGross = 360_000m,
            OldAnnualPaye = 50_000m,
            NewAnnualPaye = 50_600m,
        };

        sample.PayeDifference.Should().Be(600m, "new minus old = R600");
        sample.IsMaterialChange.Should().BeTrue("absolute difference R600 > R500 threshold");
    }

    // ── 9. RegressionSample — non-material change ────────────────────────────

    [Fact]
    public void RegressionSample_SmallChange_IsNotMaterial()
    {
        // CTL-SARS-001: Changes <= R500 are not material.
        var sample = new RegressionSample
        {
            EmployeeId = "Sample_R150000",
            AnnualGross = 150_000m,
            OldAnnualPaye = 10_000m,
            NewAnnualPaye = 10_300m,
        };

        sample.PayeDifference.Should().Be(300m);
        sample.IsMaterialChange.Should().BeFalse("R300 difference is below R500 material threshold");
    }

    // ── 10. ParsePayeJson — valid JSON produces correct rule set ──────────────

    [Fact]
    public void ParsePayeJson_ValidJson_ProducesCorrectRuleSet()
    {
        // CTL-SARS-001: Parsed rule set must have correct brackets and rebates.
        var result = TaxYearImportService.ParsePayeJson(ValidPayeJson("2026"));

        result.IsSuccess.Should().BeTrue();
        var ruleSet = result.Value;
        ruleSet.TaxYear.Should().Be("2026");
        ruleSet.Brackets.Should().HaveCount(7, "standard 2025/2026 table has 7 brackets");
        ruleSet.PrimaryRebate.Should().Be(17_235m);
        ruleSet.ThresholdBelow65.Should().Be(95_750m);
    }

    // ── 11. ParsePayeJson — invalid JSON returns failure ─────────────────────

    [Fact]
    public void ParsePayeJson_InvalidJson_ReturnsFailure()
    {
        // REQ-COMP-015: Malformed JSON must be rejected before Firestore write.
        var result = TaxYearImportService.ParsePayeJson("{ this is not valid json }");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    // ── 12. ValidatePayeJson — missing tax_year ───────────────────────────────

    [Fact]
    public void ValidatePayeJson_MissingTaxYear_ReturnsFailure()
    {
        // CTL-SARS-001: tax_year is mandatory for traceability.
        const string json = """
            {
              "tax_brackets": [
                { "min": 1, "max": 237100, "rate": 0.18, "base_tax": 0 },
                { "min": 237101, "max": 370500, "rate": 0.26, "base_tax": 42678 },
                { "min": 370501, "max": null, "rate": 0.31, "base_tax": 77362 }
              ],
              "rebates": { "primary": 17235, "secondary_age_65_plus": 9444, "tertiary_age_75_plus": 3145 }
            }
            """;

        var result = TaxYearImportService.ValidatePayeJson(json);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("tax_year");
    }

    // ── 13. BuildRegressionSamples — 5 samples always returned ──────────────

    [Fact]
    public void BuildRegressionSamples_Always_Returns5Samples()
    {
        // REQ-COMP-015: Regression must cover all 5 standard income points.
        var ruleSet = RuleSetFrom(ValidPayeJson());
        var (samples, _, _, _) = TaxYearImportService.BuildRegressionSamples(ruleSet, ruleSet);

        samples.Should().HaveCount(5, "regression always runs 5 representative income samples");
    }
}
