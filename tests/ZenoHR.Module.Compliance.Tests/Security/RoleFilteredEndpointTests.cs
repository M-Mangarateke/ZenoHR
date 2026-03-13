// VUL-009: Tests for role-filtered employee DTO responses.
// REQ-SEC-001, REQ-SEC-002, CTL-POPIA-002: Ensures different roles receive appropriately
// scoped response shapes from GET /api/employees/{id}.
// TC-SEC-030: Role-filtered endpoint returns correct DTO type per role.

using FluentAssertions;
using ZenoHR.Api.DTOs;
using EmployeeAggregate = ZenoHR.Module.Employee.Aggregates.Employee;
using ZenoHR.Module.Employee.Aggregates;

namespace ZenoHR.Module.Compliance.Tests.Security;

/// <summary>
/// Unit tests for <see cref="EmployeeDtoMapper"/> — verifies that role-based DTO mapping
/// returns the correct response shape and masks sensitive fields appropriately.
/// VUL-009: prevents data leakage by ensuring Managers and Employees never receive
/// salary, tax, banking, or national ID fields in API responses.
/// </summary>
public sealed class RoleFilteredEndpointTests
{
    // ── Test data ─────────────────────────────────────────────────────────────

    private static EmployeeAggregate CreateTestEmployee(
        string employeeId = "emp_001",
        string tenantId = "tenant_001",
        string nationalId = "8001015009082",
        string? taxRef = "1234566789",
        string systemRole = "Employee")
    {
        var result = EmployeeAggregate.Create(
            employeeId: employeeId,
            tenantId: tenantId,
            firebaseUid: "firebase_uid_001",
            legalName: "Thandi Mokoena",
            nationalIdOrPassport: nationalId,
            taxReference: taxRef,
            dateOfBirth: new DateOnly(1980, 1, 1),
            personalPhoneNumber: "+27821234567",
            personalEmail: "thandi@example.com",
            workEmail: "thandi@zenowethu.co.za",
            nationality: "ZA",
            gender: "Female",
            race: "African",
            disabilityStatus: false,
            disabilityDescription: null,
            hireDate: new DateOnly(2024, 3, 1),
            employeeType: EmployeeType.Permanent,
            departmentId: "dept_finance",
            roleId: "role_emp",
            systemRole: systemRole,
            reportsToEmployeeId: "emp_mgr_001",
            actorId: "actor_hr",
            now: DateTimeOffset.UtcNow);

        return result.Value!;
    }

    // ── Director gets EmployeeFullDto ──────────────────────────────────────────

    [Fact]
    // TC-SEC-030-001: Director receives full employee record with all fields.
    public void ToRoleDto_Director_ReturnsEmployeeFullDto()
    {
        // VUL-009
        var employee = CreateTestEmployee();

        var dto = EmployeeDtoMapper.ToRoleDto(employee, "Director");

        dto.Should().BeOfType<EmployeeFullDto>();
        var full = (EmployeeFullDto)dto;
        full.EmployeeId.Should().Be("emp_001");
        full.TenantId.Should().Be("tenant_001");
        full.LegalName.Should().Be("Thandi Mokoena");
        full.Department.Should().Be("dept_finance");
        full.PersonalEmail.Should().Be("thandi@example.com");
        full.WorkEmail.Should().Be("thandi@zenowethu.co.za");
        full.PhoneNumber.Should().Be("+27821234567");
        full.Nationality.Should().Be("ZA");
        full.Gender.Should().Be("Female");
        full.Race.Should().Be("African");
    }

    // ── HRManager gets EmployeeFullDto ─────────────────────────────────────────

    [Fact]
    // TC-SEC-030-002: HRManager receives full employee record identical to Director.
    public void ToRoleDto_HRManager_ReturnsEmployeeFullDto()
    {
        // VUL-009
        var employee = CreateTestEmployee();

        var dto = EmployeeDtoMapper.ToRoleDto(employee, "HRManager");

        dto.Should().BeOfType<EmployeeFullDto>();
        var full = (EmployeeFullDto)dto;
        full.EmployeeId.Should().Be("emp_001");
        full.LegalName.Should().Be("Thandi Mokoena");
    }

    // ── Manager gets EmployeeProfileDto ────────────────────────────────────────

    [Fact]
    // TC-SEC-030-003: Manager receives restricted profile — no salary, tax, banking, national ID.
    public void ToRoleDto_Manager_ReturnsEmployeeProfileDto()
    {
        // VUL-009
        var employee = CreateTestEmployee();

        var dto = EmployeeDtoMapper.ToRoleDto(employee, "Manager");

        dto.Should().BeOfType<EmployeeProfileDto>();
        var profile = (EmployeeProfileDto)dto;
        profile.EmployeeId.Should().Be("emp_001");
        profile.LegalName.Should().Be("Thandi Mokoena");
        profile.Department.Should().Be("dept_finance");
        profile.WorkEmail.Should().Be("thandi@zenowethu.co.za");
        profile.PhoneNumber.Should().Be("+27821234567");
    }

    // ── Employee gets EmployeeSelfDto ──────────────────────────────────────────

    [Fact]
    // TC-SEC-030-004: Employee receives minimal self-service view for own record.
    public void ToRoleDto_Employee_ReturnsEmployeeSelfDto()
    {
        // VUL-009
        var employee = CreateTestEmployee();

        var dto = EmployeeDtoMapper.ToRoleDto(employee, "Employee");

        dto.Should().BeOfType<EmployeeSelfDto>();
        var self = (EmployeeSelfDto)dto;
        self.EmployeeId.Should().Be("emp_001");
        self.LegalName.Should().Be("Thandi Mokoena");
        self.Department.Should().Be("dept_finance");
        self.WorkEmail.Should().Be("thandi@zenowethu.co.za");
    }

    // ── Unknown role defaults to EmployeeSelfDto ──────────────────────────────

    [Fact]
    // TC-SEC-030-005: Unknown/unrecognised role defaults to most restrictive DTO.
    public void ToRoleDto_UnknownRole_ReturnsEmployeeSelfDto()
    {
        // VUL-009
        var employee = CreateTestEmployee();

        var dto = EmployeeDtoMapper.ToRoleDto(employee, "SomeRandomRole");

        dto.Should().BeOfType<EmployeeSelfDto>();
    }

    // ── Masking: National ID ──────────────────────────────────────────────────

    [Fact]
    // TC-SEC-030-006: National ID is masked — first 6 + last 1 visible, middle masked.
    // CTL-POPIA-002: SA ID "8001015009082" → "800101****2"
    public void ToFullDto_MasksNationalId_First6AndLast1Visible()
    {
        // VUL-009, CTL-POPIA-002
        var employee = CreateTestEmployee(nationalId: "8001015009082");

        var dto = EmployeeDtoMapper.ToFullDto(employee);

        // SA ID: 13 digits. Mask format: first 6, "****", last 1.
        dto.NationalIdOrPassport.Should().Be("800101****2");
    }

    [Fact]
    // TC-SEC-030-007: Short national ID (< 13 chars) is fully masked.
    public void ToFullDto_ShortNationalId_FullyMasked()
    {
        // VUL-009, CTL-POPIA-002
        var employee = CreateTestEmployee(nationalId: "ABC");

        var dto = EmployeeDtoMapper.ToFullDto(employee);

        dto.NationalIdOrPassport.Should().Be("****");
    }

    // ── Masking: Tax reference ─────────────────────────────────────────────────

    [Fact]
    // TC-SEC-030-008: Tax reference masked — last 4 digits visible.
    // CTL-POPIA-002: "1234566789" → "****6789"
    public void ToFullDto_MasksTaxReference_Last4Visible()
    {
        // VUL-009, CTL-POPIA-002
        var employee = CreateTestEmployee(taxRef: "1234566789");

        var dto = EmployeeDtoMapper.ToFullDto(employee);

        dto.TaxReference.Should().Be("****6789");
    }

    [Fact]
    // TC-SEC-030-009: Null tax reference yields "****".
    public void ToFullDto_NullTaxReference_FullyMasked()
    {
        // VUL-009, CTL-POPIA-002
        var employee = CreateTestEmployee(taxRef: null);

        var dto = EmployeeDtoMapper.ToFullDto(employee);

        dto.TaxReference.Should().Be("****");
    }

    // ── Manager response excludes bank account ────────────────────────────────

    [Fact]
    // TC-SEC-030-010: Manager DTO (EmployeeProfileDto) has no bank_account_ref property.
    // VUL-009: Prevents data leakage of banking details to Manager role.
    public void ToProfileDto_ExcludesBankAccountRef()
    {
        // VUL-009
        var employee = CreateTestEmployee();

        var dto = EmployeeDtoMapper.ToProfileDto(employee);

        // EmployeeProfileDto does not have a BankAccountRef property — compile-time guarantee.
        // We verify the type is correct and has only the expected fields.
        dto.Should().BeOfType<EmployeeProfileDto>();
        var profileType = typeof(EmployeeProfileDto);
        profileType.GetProperty("BankAccountRef").Should().BeNull(
            "Manager profile DTO must not expose bank account reference");
        profileType.GetProperty("NationalIdOrPassport").Should().BeNull(
            "Manager profile DTO must not expose national ID");
        profileType.GetProperty("TaxReference").Should().BeNull(
            "Manager profile DTO must not expose tax reference");
    }

    // ── Full DTO has BankAccountRef always null (separate endpoint) ────────────

    [Fact]
    // TC-SEC-030-011: FullDto sets BankAccountRef to null — requires separate unmask endpoint (VUL-020).
    public void ToFullDto_BankAccountRef_IsNull()
    {
        // VUL-009, VUL-020
        var employee = CreateTestEmployee();

        var dto = EmployeeDtoMapper.ToFullDto(employee);

        dto.BankAccountRef.Should().BeNull(
            "Bank account details require separate unmask endpoint with purpose code (VUL-020)");
    }

    // ── Self DTO excludes all sensitive fields ─────────────────────────────────

    [Fact]
    // TC-SEC-030-012: SelfDto has no sensitive PII properties at all.
    public void ToSelfDto_ExcludesAllSensitiveFields()
    {
        // VUL-009
        var employee = CreateTestEmployee();

        var dto = EmployeeDtoMapper.ToSelfDto(employee);

        dto.Should().BeOfType<EmployeeSelfDto>();
        var selfType = typeof(EmployeeSelfDto);
        selfType.GetProperty("NationalIdOrPassport").Should().BeNull();
        selfType.GetProperty("TaxReference").Should().BeNull();
        selfType.GetProperty("BankAccountRef").Should().BeNull();
        selfType.GetProperty("PersonalEmail").Should().BeNull();
        selfType.GetProperty("PhoneNumber").Should().BeNull();
        selfType.GetProperty("Nationality").Should().BeNull();
        selfType.GetProperty("Gender").Should().BeNull();
        selfType.GetProperty("Race").Should().BeNull();
    }
}
