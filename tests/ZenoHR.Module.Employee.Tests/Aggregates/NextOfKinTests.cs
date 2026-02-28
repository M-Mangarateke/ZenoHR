// TC-HR-003: NextOfKin entity unit tests.
// REQ-HR-001, CTL-POPIA-005: Next-of-kin creation and update.

using FluentAssertions;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Employee.Aggregates;

namespace ZenoHR.Module.Employee.Tests.Aggregates;

/// <summary>
/// Unit tests for the <see cref="NextOfKin"/> entity.
/// TC-HR-003-A: Create_ValidInput_Succeeds
/// TC-HR-003-B: Create_BlankFullName_Fails
/// TC-HR-003-C: Create_UnknownRelationship_Fails
/// TC-HR-003-D: Create_BlankPhoneNumber_Fails
/// TC-HR-003-E: Update_ChangesFields
/// </summary>
public sealed class NextOfKinTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

    // ── TC-HR-003-A: Create valid ─────────────────────────────────────────────

    [Fact]
    public void Create_ValidInput_Succeeds()
    {
        var result = MakeNextOfKin("nok-001");

        result.IsSuccess.Should().BeTrue();
        result.Value!.NokId.Should().Be("nok-001");
        result.Value.Relationship.Should().Be(NokRelationship.Spouse);
        result.Value.IsPrimaryBeneficiary.Should().BeTrue();
        result.Value.Email.Should().BeNull();
    }

    // ── TC-HR-003-B: Blank FullName rejected ──────────────────────────────────

    [Fact]
    public void Create_BlankFullName_ReturnsFailure()
    {
        var result = NextOfKin.Create(
            nokId: "nok-001", tenantId: "tenant-001", employeeId: "emp-001",
            fullName: "", relationship: NokRelationship.Spouse,
            idOrPassport: null, phoneNumber: "+27821234567", email: null,
            isPrimaryBeneficiary: true, now: Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("FullName");
    }

    // ── TC-HR-003-C: Unknown relationship rejected ────────────────────────────

    [Fact]
    public void Create_UnknownRelationship_ReturnsFailure()
    {
        var result = NextOfKin.Create(
            nokId: "nok-001", tenantId: "tenant-001", employeeId: "emp-001",
            fullName: "John Doe", relationship: NokRelationship.Unknown,
            idOrPassport: null, phoneNumber: "+27821234567", email: null,
            isPrimaryBeneficiary: true, now: Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("Relationship");
    }

    // ── TC-HR-003-D: Blank phone number rejected ──────────────────────────────

    [Fact]
    public void Create_BlankPhoneNumber_ReturnsFailure()
    {
        var result = NextOfKin.Create(
            nokId: "nok-001", tenantId: "tenant-001", employeeId: "emp-001",
            fullName: "John Doe", relationship: NokRelationship.Spouse,
            idOrPassport: null, phoneNumber: "   ", email: null,
            isPrimaryBeneficiary: true, now: Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("PhoneNumber");
    }

    // ── TC-HR-003-E: Update changes fields ────────────────────────────────────

    [Fact]
    public void Update_ValidInput_ChangesFields()
    {
        var nok = MakeNextOfKin("nok-001").Value!;
        var updateNow = Now.AddDays(7);

        nok.Update("Jane Doe Updated", NokRelationship.Child, null, "+27829876543", "jane@test.com",
            false, updateNow);

        nok.FullName.Should().Be("Jane Doe Updated");
        nok.Relationship.Should().Be(NokRelationship.Child);
        nok.Email.Should().Be("jane@test.com");
        nok.IsPrimaryBeneficiary.Should().BeFalse();
        nok.UpdatedAt.Should().Be(updateNow);
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static Result<NextOfKin> MakeNextOfKin(string id) =>
        NextOfKin.Create(
            nokId: id,
            tenantId: "tenant-001",
            employeeId: "emp-001",
            fullName: "John Doe",
            relationship: NokRelationship.Spouse,
            idOrPassport: null,
            phoneNumber: "+27821234567",
            email: null,
            isPrimaryBeneficiary: true,
            now: Now);
}
