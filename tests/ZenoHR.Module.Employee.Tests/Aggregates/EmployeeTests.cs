// TC-HR-001: Employee aggregate unit tests.
// REQ-HR-001: Employee lifecycle — create, update profile, suspend, reactivate, terminate.
// REQ-SEC-005: Tenant ID enforced at construction.

using FluentAssertions;
using ZenoHR.Domain.Errors;
using Emp = ZenoHR.Module.Employee.Aggregates.Employee;
using ZenoHR.Module.Employee.Aggregates;

namespace ZenoHR.Module.Employee.Tests.Aggregates;

/// <summary>
/// Unit tests for the <see cref="Employee"/> aggregate.
/// TC-HR-001-A: Create_ValidInput_Succeeds
/// TC-HR-001-B: Create_BlankTenantId_Fails
/// TC-HR-001-C: Create_BlankLegalName_Fails
/// TC-HR-001-D: UpdateProfile_ValidInput_UpdatesFields
/// TC-HR-001-E: Terminate_ActiveEmployee_Succeeds
/// TC-HR-001-F: Terminate_AlreadyTerminated_Fails
/// TC-HR-001-G: Suspend_ActiveEmployee_Succeeds
/// TC-HR-001-H: Reactivate_SuspendedEmployee_Succeeds
/// </summary>
public sealed class EmployeeTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

    // ── TC-HR-001-A: Create valid employee ───────────────────────────────────

    [Fact]
    public void Create_ValidInput_Succeeds()
    {
        var result = MakeEmployee("emp-001", "tenant-001");

        result.IsSuccess.Should().BeTrue();
        result.Value!.EmployeeId.Should().Be("emp-001");
        result.Value.TenantId.Should().Be("tenant-001");
        result.Value.LegalName.Should().Be("Jane Doe");
        result.Value.EmploymentStatus.Should().Be(EmploymentStatus.Active);
        result.Value.CreatedAt.Should().Be(Now);
    }

    // ── TC-HR-001-B: Blank TenantId rejected ─────────────────────────────────

    [Fact]
    public void Create_BlankTenantId_ReturnsFailure()
    {
        var result = Emp.Create(
            employeeId: "emp-001", tenantId: "",
            firebaseUid: "uid-001", legalName: "Jane Doe",
            nationalIdOrPassport: "8001015009087", taxReference: "9123456789",
            dateOfBirth: new DateOnly(1980, 1, 1), personalPhoneNumber: "+27821234567",
            personalEmail: "jane@zenowethu.co.za", workEmail: null,
            nationality: "ZA", gender: "Female", race: "African",
            disabilityStatus: false, disabilityDescription: null,
            hireDate: new DateOnly(2023, 1, 1), employeeType: EmployeeType.Permanent,
            departmentId: "dept-001", roleId: "role-001", systemRole: "Employee",
            reportsToEmployeeId: null, actorId: "actor-001", now: Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("TenantId");
    }

    // ── TC-HR-001-C: Blank LegalName rejected ────────────────────────────────

    [Fact]
    public void Create_BlankLegalName_ReturnsFailure()
    {
        var result = Emp.Create(
            employeeId: "emp-001", tenantId: "tenant-001",
            firebaseUid: "uid-001", legalName: "   ",
            nationalIdOrPassport: "8001015009087", taxReference: null,
            dateOfBirth: new DateOnly(1980, 1, 1), personalPhoneNumber: "+27821234567",
            personalEmail: "jane@zenowethu.co.za", workEmail: null,
            nationality: "ZA", gender: "Female", race: "African",
            disabilityStatus: false, disabilityDescription: null,
            hireDate: new DateOnly(2023, 1, 1), employeeType: EmployeeType.Permanent,
            departmentId: "dept-001", roleId: "role-001", systemRole: "Employee",
            reportsToEmployeeId: null, actorId: "actor-001", now: Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("LegalName");
    }

    // ── TC-HR-001-D: UpdateProfile valid ─────────────────────────────────────

    [Fact]
    public void UpdateProfile_ValidInput_UpdatesFields()
    {
        var emp = MakeEmployee("emp-001", "tenant-001").Value!;
        var updateNow = Now.AddDays(30);

        var result = emp.UpdateProfile(
            legalName: "Jane Updated",
            personalPhoneNumber: "+27829876543",
            personalEmail: "jane.updated@zenowethu.co.za",
            workEmail: "jane.work@zenowethu.co.za",
            maritalStatus: "Married",
            taxReference: "9999999999",
            bankAccountRef: "ba-001",
            actorId: "actor-001",
            now: updateNow);

        result.IsSuccess.Should().BeTrue();
        emp.LegalName.Should().Be("Jane Updated");
        emp.UpdatedAt.Should().Be(updateNow);
    }

    // ── TC-HR-001-E: Terminate active employee ───────────────────────────────

    [Fact]
    public void Terminate_ActiveEmployee_Succeeds()
    {
        var emp = MakeEmployee("emp-001", "tenant-001").Value!;

        var result = emp.Terminate("RESIGNATION", new DateOnly(2026, 3, 31), "actor-001", Now);

        result.IsSuccess.Should().BeTrue();
        emp.EmploymentStatus.Should().Be(EmploymentStatus.Terminated);
    }

    // ── TC-HR-001-F: Terminate already-terminated fails ──────────────────────

    [Fact]
    public void Terminate_AlreadyTerminated_ReturnsFailure()
    {
        var emp = MakeEmployee("emp-001", "tenant-001").Value!;
        emp.Terminate("RESIGNATION", new DateOnly(2026, 3, 31), "actor-001", Now);

        var result = emp.Terminate("REDUNDANCY", new DateOnly(2026, 4, 1), "actor-001", Now);

        result.IsFailure.Should().BeTrue();
    }

    // ── TC-HR-001-G: Suspend active employee ─────────────────────────────────

    [Fact]
    public void Suspend_ActiveEmployee_Succeeds()
    {
        var emp = MakeEmployee("emp-001", "tenant-001").Value!;

        var result = emp.Suspend("MISCONDUCT_INVESTIGATION", "actor-001", Now);

        result.IsSuccess.Should().BeTrue();
        emp.EmploymentStatus.Should().Be(EmploymentStatus.Suspended);
    }

    // ── TC-HR-001-H: Reactivate suspended employee ───────────────────────────

    [Fact]
    public void Reactivate_SuspendedEmployee_Succeeds()
    {
        var emp = MakeEmployee("emp-001", "tenant-001").Value!;
        emp.Suspend("MISCONDUCT_INVESTIGATION", "actor-001", Now);

        var result = emp.Reactivate("actor-001", Now);

        result.IsSuccess.Should().BeTrue();
        emp.EmploymentStatus.Should().Be(EmploymentStatus.Active);
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static Result<Emp> MakeEmployee(
        string empId, string tenantId) =>
        Emp.Create(
            employeeId: empId,
            tenantId: tenantId,
            firebaseUid: $"uid_{empId}",
            legalName: "Jane Doe",
            nationalIdOrPassport: "8001015009087",
            taxReference: "9123456789",
            dateOfBirth: new DateOnly(1980, 1, 1),
            personalPhoneNumber: "+27821234567",
            personalEmail: $"{empId}@zenowethu.co.za",
            workEmail: null,
            nationality: "ZA",
            gender: "Female",
            race: "African",
            disabilityStatus: false,
            disabilityDescription: null,
            hireDate: new DateOnly(2023, 1, 1),
            employeeType: EmployeeType.Permanent,
            departmentId: "dept-001",
            roleId: "role-001",
            systemRole: "Employee",
            reportsToEmployeeId: null,
            actorId: "actor-001",
            now: Now);
}
