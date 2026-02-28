// TC-HR-004: EmployeeBenefit entity unit tests.
// REQ-HR-001, REQ-HR-003: Benefit creation, contribution rates, deactivation.

using FluentAssertions;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Employee.Aggregates;

namespace ZenoHR.Module.Employee.Tests.Aggregates;

/// <summary>
/// Unit tests for the <see cref="EmployeeBenefit"/> entity.
/// TC-HR-004-A: Create_ValidInput_Succeeds (IsActive=true on creation)
/// TC-HR-004-B: Create_UnknownBenefitType_Fails
/// TC-HR-004-C: Create_NegativeEmployeeContribution_Fails
/// TC-HR-004-D: Create_ContributionRateOver100Percent_Fails
/// TC-HR-004-E: Deactivate_SetsIsActiveFalseAndEffectiveTo
/// TC-HR-004-F: UpdateRates_ChangesContributionRates
/// </summary>
public sealed class EmployeeBenefitTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

    // ── TC-HR-004-A: Create valid benefit ─────────────────────────────────────

    [Fact]
    public void Create_ValidInput_IsActiveTrueAndRatesSet()
    {
        var result = MakeBenefit("ben-001", BenefitType.MedicalAid);

        result.IsSuccess.Should().BeTrue();
        result.Value!.BenefitId.Should().Be("ben-001");
        result.Value.BenefitType.Should().Be(BenefitType.MedicalAid);
        result.Value.IsActive.Should().BeTrue();
        result.Value.EmployeeContributionRate.Should().Be(0.075m);
        result.Value.EmployerContributionRate.Should().Be(0.075m);
        result.Value.EffectiveTo.Should().BeNull();
    }

    // ── TC-HR-004-B: Unknown benefit type rejected ────────────────────────────

    [Fact]
    public void Create_UnknownBenefitType_ReturnsFailure()
    {
        var result = EmployeeBenefit.Create(
            benefitId: "ben-001", tenantId: "tenant-001", employeeId: "emp-001",
            benefitType: BenefitType.Unknown, providerName: "Discovery",
            membershipNumber: "M-001", planName: "Classic",
            employeeContributionRate: 0.075m, employerContributionRate: 0.075m,
            effectiveFrom: new DateOnly(2026, 1, 1), now: Now);

        result.IsFailure.Should().BeTrue();
    }

    // ── TC-HR-004-C: Negative rate rejected ──────────────────────────────────

    [Fact]
    public void Create_NegativeEmployeeContributionRate_ReturnsFailure()
    {
        var result = EmployeeBenefit.Create(
            benefitId: "ben-001", tenantId: "tenant-001", employeeId: "emp-001",
            benefitType: BenefitType.PensionFund, providerName: "Old Mutual",
            membershipNumber: "P-001", planName: "Growth",
            employeeContributionRate: -0.05m, employerContributionRate: 0.075m,
            effectiveFrom: new DateOnly(2026, 1, 1), now: Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("EmployeeContributionRate");
    }

    // ── TC-HR-004-D: Rate over 100% rejected ─────────────────────────────────

    [Fact]
    public void Create_ContributionRateOverOne_ReturnsFailure()
    {
        var result = EmployeeBenefit.Create(
            benefitId: "ben-001", tenantId: "tenant-001", employeeId: "emp-001",
            benefitType: BenefitType.MedicalAid, providerName: "Bonitas",
            membershipNumber: "M-002", planName: "Primary",
            employeeContributionRate: 0.075m, employerContributionRate: 1.5m,
            effectiveFrom: new DateOnly(2026, 1, 1), now: Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("EmployerContributionRate");
    }

    // ── TC-HR-004-E: Deactivate sets IsActive=false ───────────────────────────

    [Fact]
    public void Deactivate_SetsIsActiveFalseAndEffectiveTo()
    {
        var benefit = MakeBenefit("ben-001", BenefitType.MedicalAid).Value!;
        var effectiveTo = new DateOnly(2026, 6, 30);

        benefit.Deactivate(effectiveTo, Now);

        benefit.IsActive.Should().BeFalse();
        benefit.EffectiveTo.Should().Be(effectiveTo);
    }

    // ── TC-HR-004-F: UpdateRates changes contribution rates ───────────────────

    [Fact]
    public void UpdateRates_ChangesRates()
    {
        var benefit = MakeBenefit("ben-001", BenefitType.PensionFund).Value!;

        benefit.UpdateRates(0.10m, 0.12m, Now);

        benefit.EmployeeContributionRate.Should().Be(0.10m);
        benefit.EmployerContributionRate.Should().Be(0.12m);
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static Result<EmployeeBenefit> MakeBenefit(
        string id, BenefitType type) =>
        EmployeeBenefit.Create(
            benefitId: id,
            tenantId: "tenant-001",
            employeeId: "emp-001",
            benefitType: type,
            providerName: "Discovery Health",
            membershipNumber: "M-123456",
            planName: "Classic Plan",
            employeeContributionRate: 0.075m,
            employerContributionRate: 0.075m,
            effectiveFrom: new DateOnly(2026, 1, 1),
            now: Now);
}
