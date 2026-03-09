// CTL-SARS-001, REQ-HR-003, REQ-SEC-002
// Unit tests for StatutoryFieldPermissions — the whitelist that controls which rule_data
// fields an HR Manager / Director may update via the Settings UI.
// These tests run without Firestore — pure domain logic.
// Covers both the field whitelist (GetAllowedFields/GetDisallowedFields/IsFieldAllowed)
// and the field spec/rendering metadata (GetFieldSpecs, FieldEditType).

using FluentAssertions;
using ZenoHR.Domain.Common;

namespace ZenoHR.Module.Compliance.Tests;

/// <summary>
/// Verifies that the field whitelist in <see cref="StatutoryFieldPermissions"/> correctly
/// gates access to statutory rule set fields — allowing HR self-service updates for
/// PROVISIONAL figures while protecting SARS tax brackets from accidental mutation.
/// CTL-SARS-001
/// </summary>
public sealed class StatutoryFieldPermissionsTests
{
    // ── GetAllowedFields ─────────────────────────────────────────────────────

    [Fact]
    public void GetAllowedFields_BceaEarningsThreshold_IncludesAllNumericFields()
    {
        // Arrange / Act
        var allowed = StatutoryFieldPermissions.GetAllowedFields(RuleDomains.BceaEarningsThreshold);

        // Assert
        allowed.Should().Contain("annual_threshold");
        allowed.Should().Contain("monthly_threshold");
        allowed.Should().Contain("weekly_threshold");
        allowed.Should().Contain("daily_threshold");
        allowed.Should().Contain("data_status");
    }

    [Fact]
    public void GetAllowedFields_Nmw_IncludesDataStatusOnly()
    {
        var allowed = StatutoryFieldPermissions.GetAllowedFields(RuleDomains.Nmw);

        allowed.Should().Contain("data_status");
    }

    [Theory]
    [InlineData(RuleDomains.SarsEti)]
    [InlineData(RuleDomains.BceaLeave)]
    [InlineData(RuleDomains.BceaWorkingTime)]
    [InlineData(RuleDomains.BceaNoticeSeverance)]
    [InlineData(RuleDomains.SaPublicHolidays)]
    public void GetAllowedFields_LockedDomains_AllowDataStatusOnly(string ruleDomain)
    {
        var allowed = StatutoryFieldPermissions.GetAllowedFields(ruleDomain);

        // ETI and perpetual BCEA domains: only data_status is editable via UI
        // SARS annually-changing domains now have full field whitelists (expanded in v2)
        allowed.Should().BeEquivalentTo(new[] { "data_status" });
    }

    // ── GetDisallowedFields ──────────────────────────────────────────────────

    [Fact]
    public void GetDisallowedFields_BceaThreshold_AllowedFieldsReturnsEmpty()
    {
        var requested = new[] { "annual_threshold", "monthly_threshold", "data_status" };

        var disallowed = StatutoryFieldPermissions.GetDisallowedFields(
            RuleDomains.BceaEarningsThreshold, requested);

        disallowed.Should().BeEmpty();
    }

    [Fact]
    public void GetDisallowedFields_SarsPaye_AllowedFieldsReturnsEmpty()
    {
        var requested = new[] { "tax_brackets", "rebates", "data_status" };

        var disallowed = StatutoryFieldPermissions.GetDisallowedFields(
            RuleDomains.SarsPaye, requested);

        // tax_brackets and rebates are now editable (whitelist expanded in v2)
        disallowed.Should().BeEmpty();
    }

    [Fact]
    public void GetDisallowedFields_BceaThreshold_NonExistentFieldIsDisallowed()
    {
        var requested = new[] { "annual_threshold", "invalid_field" };

        var disallowed = StatutoryFieldPermissions.GetDisallowedFields(
            RuleDomains.BceaEarningsThreshold, requested);

        disallowed.Should().ContainSingle().Which.Should().Be("invalid_field");
    }

    [Fact]
    public void GetDisallowedFields_EmptyRequest_ReturnsEmpty()
    {
        var disallowed = StatutoryFieldPermissions.GetDisallowedFields(
            RuleDomains.SarsPaye, []);

        disallowed.Should().BeEmpty();
    }

    // ── IsFieldAllowed ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("annual_threshold",  true)]
    [InlineData("monthly_threshold", true)]
    [InlineData("weekly_threshold",  true)]
    [InlineData("daily_threshold",   true)]
    [InlineData("data_status",       true)]
    [InlineData("tax_year",          false)]
    [InlineData("source",            false)]
    public void IsFieldAllowed_BceaEarningsThreshold_CorrectlyGates(string field, bool shouldBeAllowed)
    {
        var result = StatutoryFieldPermissions.IsFieldAllowed(
            RuleDomains.BceaEarningsThreshold, field);

        result.Should().Be(shouldBeAllowed);
    }

    [Theory]
    [InlineData("data_status",  true)]
    [InlineData("tax_brackets", true)]
    [InlineData("rebates",      true)]
    [InlineData("tax_year",     false)]
    public void IsFieldAllowed_SarsPaye_AnnuallyChangingFieldsAllowed(string field, bool shouldBeAllowed)
    {
        var result = StatutoryFieldPermissions.IsFieldAllowed(RuleDomains.SarsPaye, field);

        result.Should().Be(shouldBeAllowed);
    }

    // ── Case insensitivity ───────────────────────────────────────────────────

    [Theory]
    [InlineData("ANNUAL_THRESHOLD")]
    [InlineData("Annual_Threshold")]
    [InlineData("annual_threshold")]
    public void GetDisallowedFields_BceaThreshold_CaseInsensitiveFieldNames(string fieldName)
    {
        // Field name matching should be case-insensitive
        var disallowed = StatutoryFieldPermissions.GetDisallowedFields(
            RuleDomains.BceaEarningsThreshold, [fieldName]);

        disallowed.Should().BeEmpty($"'{fieldName}' should be allowed regardless of casing");
    }

    [Theory]
    [InlineData("BCEA_EARNINGS_THRESHOLD")]
    [InlineData("bcea_earnings_threshold")]
    [InlineData("Bcea_Earnings_Threshold")]
    public void GetAllowedFields_BceaThreshold_CaseInsensitiveRuleDomain(string ruleDomain)
    {
        // Rule domain lookup should be case-insensitive
        var allowed = StatutoryFieldPermissions.GetAllowedFields(ruleDomain);

        allowed.Should().Contain("annual_threshold");
    }
}

/// <summary>
/// Tests for the expanded SARS field whitelist — verifies that all annually-changing
/// statutory figures are now editable via the Settings UI without developer intervention.
/// CTL-SARS-001, REQ-HR-003
/// </summary>
public sealed class StatutoryFieldPermissionsExpandedTests
{
    // ── GetAllowedFields: SARS domains now fully editable ────────────────────

    [Fact]
    public void GetAllowedFields_SarsPaye_IncludesTaxBrackets()
    {
        var allowed = StatutoryFieldPermissions.GetAllowedFields(RuleDomains.SarsPaye);

        allowed.Should().Contain("tax_brackets");
        allowed.Should().Contain("rebates");
        allowed.Should().Contain("tax_thresholds");
        allowed.Should().Contain("retirement_fund_deduction");
        allowed.Should().Contain("data_status");
    }

    [Fact]
    public void GetAllowedFields_SarsMsftc_IncludesMonthlyAndAnnualCredits()
    {
        var allowed = StatutoryFieldPermissions.GetAllowedFields(RuleDomains.SarsMsftc);

        allowed.Should().Contain("monthly_credits");
        allowed.Should().Contain("annual_credits");
        allowed.Should().Contain("data_status");
    }

    [Fact]
    public void GetAllowedFields_SarsTravel_IncludesFixedCostTable()
    {
        var allowed = StatutoryFieldPermissions.GetAllowedFields(RuleDomains.SarsTravel);

        allowed.Should().Contain("reimbursive_rate_rands_per_km");
        allowed.Should().Contain("fixed_cost_table");
        allowed.Should().Contain("subsistence_allowance");
        allowed.Should().Contain("data_status");
    }

    [Fact]
    public void GetAllowedFields_SarsUifSdl_IncludesUifAndSdl()
    {
        var allowed = StatutoryFieldPermissions.GetAllowedFields(RuleDomains.SarsUifSdl);

        allowed.Should().Contain("uif");
        allowed.Should().Contain("sdl");
        allowed.Should().Contain("data_status");
    }

    [Fact]
    public void GetAllowedFields_Nmw_IncludesAllWorkerCategories()
    {
        var allowed = StatutoryFieldPermissions.GetAllowedFields(RuleDomains.Nmw);

        allowed.Should().Contain("general_workers");
        allowed.Should().Contain("domestic_workers");
        allowed.Should().Contain("farm_workers");
        allowed.Should().Contain("expanded_public_works_programme");
        allowed.Should().Contain("eti_relevance");
    }

    [Fact]
    public void GetDisallowedFields_SarsPaye_TaxBracketsNowAllowed()
    {
        // Previously locked — now editable. This test confirms the expansion.
        var disallowed = StatutoryFieldPermissions.GetDisallowedFields(
            RuleDomains.SarsPaye, ["tax_brackets", "rebates", "tax_thresholds"]);

        disallowed.Should().BeEmpty("SARS PAYE annually-changing fields must be editable via Settings UI");
    }

    [Fact]
    public void GetDisallowedFields_SarsPaye_SourceAndDomainStillProtected()
    {
        // Structural metadata must remain locked regardless of whitelist expansion
        var disallowed = StatutoryFieldPermissions.GetDisallowedFields(
            RuleDomains.SarsPaye, ["source", "source_url", "rule_domain", "tax_year"]);

        disallowed.Should().HaveCount(4, "structural/metadata fields must never be editable");
    }

    // ── GetFieldSpecs: rendering metadata ────────────────────────────────────

    [Fact]
    public void GetFieldSpecs_SarsPaye_ReturnsFourSpecs()
    {
        var specs = StatutoryFieldPermissions.GetFieldSpecs(RuleDomains.SarsPaye);

        specs.Should().HaveCount(4);
        specs.Select(s => s.Key).Should().Contain("tax_brackets");
        specs.Select(s => s.Key).Should().Contain("rebates");
    }

    [Fact]
    public void GetFieldSpecs_SarsPaye_TaxBracketsIsArrayOfObjectsType()
    {
        var spec = StatutoryFieldPermissions.GetFieldSpecs(RuleDomains.SarsPaye)
            .Single(s => s.Key == "tax_brackets");

        spec.EditType.Should().Be(FieldEditType.ArrayOfObjects,
            "tax brackets are a list of 7 bracket objects — require table-based editing");
    }

    [Fact]
    public void GetFieldSpecs_SarsPaye_RebasesIsNestedObjectType()
    {
        var spec = StatutoryFieldPermissions.GetFieldSpecs(RuleDomains.SarsPaye)
            .Single(s => s.Key == "rebates");

        spec.EditType.Should().Be(FieldEditType.NestedObject);
    }

    [Fact]
    public void GetFieldSpecs_SarsTravel_FixedCostTableIsArrayOfObjectsType()
    {
        var spec = StatutoryFieldPermissions.GetFieldSpecs(RuleDomains.SarsTravel)
            .Single(s => s.Key == "fixed_cost_table");

        spec.EditType.Should().Be(FieldEditType.ArrayOfObjects,
            "fixed cost table has 9 vehicle-value bands — requires table-based editing");
    }

    [Fact]
    public void GetFieldSpecs_SarsTravel_ReimbursiveRateIsScalar()
    {
        var spec = StatutoryFieldPermissions.GetFieldSpecs(RuleDomains.SarsTravel)
            .Single(s => s.Key == "reimbursive_rate_rands_per_km");

        spec.EditType.Should().Be(FieldEditType.Scalar);
    }

    [Fact]
    public void GetFieldSpecs_SarsUifSdl_UifIsNestedObject()
    {
        var spec = StatutoryFieldPermissions.GetFieldSpecs(RuleDomains.SarsUifSdl)
            .Single(s => s.Key == "uif");

        spec.EditType.Should().Be(FieldEditType.NestedObject);
    }

    [Fact]
    public void GetFieldSpecs_BceaEarningsThreshold_AllFieldsAreScalar()
    {
        var specs = StatutoryFieldPermissions.GetFieldSpecs(RuleDomains.BceaEarningsThreshold);

        specs.Should().NotBeEmpty();
        specs.Should().AllSatisfy(s => s.EditType.Should().Be(FieldEditType.Scalar,
            because: "BCEA threshold values are individual numeric figures"));
    }

    [Fact]
    public void GetFieldSpecs_SarsEti_ReturnsEmptyList()
    {
        // ETI is locked — no UI editing, only data_status via universal fallback
        var specs = StatutoryFieldPermissions.GetFieldSpecs(RuleDomains.SarsEti);

        specs.Should().BeEmpty("ETI tiers change infrequently and require legal review");
    }

    [Fact]
    public void GetFieldSpecs_Nmw_GeneralWorkersIsNestedObject()
    {
        var spec = StatutoryFieldPermissions.GetFieldSpecs(RuleDomains.Nmw)
            .Single(s => s.Key == "general_workers");

        spec.EditType.Should().Be(FieldEditType.NestedObject,
            "NMW general workers has multiple derived rate fields");
    }
}
