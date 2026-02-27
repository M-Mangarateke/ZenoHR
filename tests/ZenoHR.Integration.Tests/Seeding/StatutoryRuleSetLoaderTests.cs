// TC-OPS-004: Statutory seed data loader integration tests.
// Verifies that all seed data JSON files are properly embedded and can be loaded into
// the Firestore emulator. This is critical because payroll calculations depend on these
// documents — if seeding fails, the entire payroll engine is broken.
// CTL-SARS-001: PAYE, UIF/SDL, ETI rules must exist in Firestore before any payroll run.

using FluentAssertions;
using Google.Cloud.Firestore;
using ZenoHR.Domain.Common;
using ZenoHR.Infrastructure.Seeding;
using ZenoHR.Integration.Tests.Infrastructure;

namespace ZenoHR.Integration.Tests.Seeding;

/// <summary>
/// Integration tests for StatutoryRuleSetLoader.
/// Verifies seed data is correctly written to and readable from the Firestore emulator.
/// TC-OPS-004: Seed data loader writes all 7 statutory rule sets.
/// CTL-SARS-001: PAYE rule set readable with correct tax brackets.
/// CTL-BCEA-003: BCEA leave rule set readable with correct annual leave entitlement.
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class StatutoryRuleSetLoaderTests : IntegrationTestBase
{
    private readonly StatutoryRuleSetLoader _loader;

    public StatutoryRuleSetLoaderTests(FirestoreEmulatorFixture fixture) : base(fixture)
    {
        _loader = new StatutoryRuleSetLoader(fixture.Db);
    }

    // TC-OPS-004: All 7 seed files are seeded successfully
    [Fact]
    public async Task LoadAllAsync_AllSeedFiles_WritesAllDocuments()
    {
        // Act
        var result = await _loader.LoadAllAsync();

        // Assert
        result.IsSuccess.Should().BeTrue(because: $"seed loading failed: {(result.IsSuccess ? "" : result.Error.Message)}");
        result.Value.Should().Be(7, because: "there are 7 statutory seed files");
    }

    // TC-OPS-004: Document IDs are deterministic and follow the expected format
    [Fact]
    public void GetExpectedDocumentIds_ReturnsAllExpectedIds()
    {
        // Act
        var ids = StatutoryRuleSetLoader.GetExpectedDocumentIds();

        // Assert
        ids.Should().HaveCount(7);
        ids.Should().Contain("SARS_PAYE_2026");
        ids.Should().Contain("SARS_UIF_SDL_2026");
        ids.Should().Contain("SARS_ETI_2026");
        ids.Should().Contain("BCEA_LEAVE_2026");
        ids.Should().Contain("BCEA_WORKING_TIME_2026");
        ids.Should().Contain("BCEA_NOTICE_SEVERANCE_2026");
        ids.Should().Contain("SA_PUBLIC_HOLIDAYS_2026");
    }

    // CTL-SARS-001: PAYE rule set is readable after seeding
    [Fact]
    public async Task LoadAllAsync_SarsPaye_DocumentContainsTaxBrackets()
    {
        // Arrange
        await _loader.LoadAllAsync();

        // Act
        var snap = await Db.Collection("statutory_rule_sets")
            .Document("SARS_PAYE_2026")
            .GetSnapshotAsync();

        // Assert
        snap.Exists.Should().BeTrue("SARS_PAYE_2026 must exist after seeding");

        var data = snap.ToDictionary();
        data["rule_domain"].Should().Be("SARS_PAYE");
        data["tax_year"].Should().Be("2026");
        data["tenant_id"].Should().Be("SYSTEM");

        // Verify rule_data contains tax_brackets
        var ruleData = data["rule_data"] as IDictionary<string, object>;
        ruleData.Should().NotBeNull("rule_data must be a nested map");
        ruleData!.Should().ContainKey("tax_brackets", because: "PAYE rule set must have tax brackets");

        var brackets = ruleData["tax_brackets"] as IList<object>;
        brackets.Should().NotBeNull();
        brackets!.Count.Should().Be(7, because: "2025/26 tax year has 7 brackets");
    }

    // CTL-SARS-001: UIF/SDL rule set is readable and has correct values
    [Fact]
    public async Task LoadAllAsync_SarsUifSdl_DocumentContainsUifCeiling()
    {
        // Arrange
        await _loader.LoadAllAsync();

        // Act
        var snap = await Db.Collection("statutory_rule_sets")
            .Document("SARS_UIF_SDL_2026")
            .GetSnapshotAsync();

        // Assert
        snap.Exists.Should().BeTrue();
        var ruleData = snap.GetValue<Dictionary<string, object>>("rule_data");

        var uif = ruleData["uif"] as IDictionary<string, object>;
        uif.Should().NotBeNull();
        // UIF monthly ceiling is R17,712 (critical — must not be hardcoded in engine)
        var ceiling = Convert.ToDecimal(uif!["monthly_ceiling"]);
        ceiling.Should().Be(17712.00m, because: "UIF ceiling is R17,712/month per UICA 2002");
    }

    // CTL-BCEA-003: BCEA leave rule set contains annual leave entitlement
    [Fact]
    public async Task LoadAllAsync_BceaLeave_DocumentContainsAnnualLeaveEntitlement()
    {
        // Arrange
        await _loader.LoadAllAsync();

        // Act
        var snap = await Db.Collection("statutory_rule_sets")
            .Document("BCEA_LEAVE_2026")
            .GetSnapshotAsync();

        // Assert
        snap.Exists.Should().BeTrue();
        var ruleData = snap.GetValue<Dictionary<string, object>>("rule_data");

        var annualLeave = ruleData["annual_leave"] as IDictionary<string, object>;
        annualLeave.Should().NotBeNull();
        // 21 consecutive days = 15 working days (BCEA Section 20)
        var entitlement = Convert.ToInt64(annualLeave!["entitlement_consecutive_days"]);
        entitlement.Should().Be(21, because: "BCEA Section 20 mandates 21 consecutive days annual leave");
    }

    // TC-OPS-004: Seeding is idempotent — running twice does not fail
    [Fact]
    public async Task LoadAllAsync_CalledTwice_IsIdempotent()
    {
        // Act
        var first = await _loader.LoadAllAsync();
        var second = await _loader.LoadAllAsync();

        // Assert — both runs succeed; Firestore SetAsync upserts
        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.Should().Be(second.Value);
    }
}
