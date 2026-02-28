// TC-HR-001: EmployeeRepository integration tests.
// REQ-HR-001: Employee CRUD, tenant isolation, department scoping.
// REQ-SEC-005: Tenant ID always resolved from context — cross-tenant access blocked.

using FluentAssertions;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Integration.Tests.Infrastructure;
using ZenoHR.Module.Employee.Aggregates;

namespace ZenoHR.Integration.Tests.Employee;

/// <summary>
/// Integration tests for <see cref="EmployeeRepository"/> against the Firestore emulator.
/// TC-HR-001-A: Create and retrieve employee.
/// TC-HR-001-B: Tenant isolation — different tenant cannot read employee.
/// TC-HR-001-C: ListByDepartment scoping.
/// TC-HR-001-D: ListActive returns only active employees.
/// TC-HR-001-E: GetByFirebaseUid returns correct employee.
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class EmployeeRepositoryTests : IntegrationTestBase
{
    private readonly EmployeeRepository _repo;

    public EmployeeRepositoryTests(FirestoreEmulatorFixture fixture) : base(fixture)
    {
        _repo = new EmployeeRepository(fixture.Db);
    }

    // ── TC-HR-001-A: Create and retrieve ─────────────────────────────────────

    [Fact]
    public async Task SaveAsync_ThenGetByEmployeeId_ReturnsEmployee()
    {
        // Arrange
        var emp = CreateEmployee($"emp_{Guid.CreateVersion7()}", TenantId, "dept-001");

        // Act
        var saveResult = await _repo.SaveAsync(emp);
        var getResult = await _repo.GetByEmployeeIdAsync(TenantId, emp.EmployeeId);

        // Assert
        saveResult.IsSuccess.Should().BeTrue();
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value!.EmployeeId.Should().Be(emp.EmployeeId);
        getResult.Value.LegalName.Should().Be(emp.LegalName);
        getResult.Value.DepartmentId.Should().Be(emp.DepartmentId);
    }

    // ── TC-HR-001-B: Tenant isolation ────────────────────────────────────────

    [Fact]
    public async Task GetByEmployeeIdAsync_WrongTenant_ReturnsFailure()
    {
        // Arrange
        var emp = CreateEmployee($"emp_{Guid.CreateVersion7()}", TenantId, "dept-001");
        await _repo.SaveAsync(emp);

        // Act — query with a different tenant
        var result = await _repo.GetByEmployeeIdAsync("other-tenant", emp.EmployeeId);

        // Assert
        result.IsFailure.Should().BeTrue(because: "cross-tenant access must be rejected");
    }

    // ── TC-HR-001-C: Department scoping ──────────────────────────────────────

    [Fact]
    public async Task ListByDepartmentAsync_ReturnsOnlyMatchingDepartment()
    {
        // Arrange
        var deptA = "dept-aaa";
        var deptB = "dept-bbb";

        var empA1 = CreateEmployee($"emp_{Guid.CreateVersion7()}", TenantId, deptA);
        var empA2 = CreateEmployee($"emp_{Guid.CreateVersion7()}", TenantId, deptA);
        var empB1 = CreateEmployee($"emp_{Guid.CreateVersion7()}", TenantId, deptB);

        await _repo.SaveAsync(empA1);
        await _repo.SaveAsync(empA2);
        await _repo.SaveAsync(empB1);

        // Act
        var result = await _repo.ListByDepartmentAsync(TenantId, deptA);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(e => e.DepartmentId == deptA);
    }

    // ── TC-HR-001-D: ListActive returns only active employees ─────────────────

    [Fact]
    public async Task ListActiveAsync_ReturnsOnlyActiveEmployees()
    {
        // Arrange
        var active = CreateEmployee($"emp_{Guid.CreateVersion7()}", TenantId, "dept-001");
        await _repo.SaveAsync(active);

        // Terminate one employee
        var terminated = CreateEmployee($"emp_{Guid.CreateVersion7()}", TenantId, "dept-001");
        await _repo.SaveAsync(terminated);
        terminated.Terminate("RESIGNATION", DateOnly.FromDateTime(DateTime.UtcNow), "actor-001", DateTimeOffset.UtcNow);
        await _repo.SaveAsync(terminated);

        // Act
        var result = await _repo.ListActiveAsync(TenantId);

        // Assert — must include active, must not include terminated
        result.Should().Contain(e => e.EmployeeId == active.EmployeeId);
        result.Should().NotContain(e => e.EmployeeId == terminated.EmployeeId);
    }

    // ── TC-HR-001-E: GetByFirebaseUid ─────────────────────────────────────────

    [Fact]
    public async Task GetByFirebaseUidAsync_ReturnsCorrectEmployee()
    {
        // Arrange
        var uid = $"uid_{Guid.NewGuid():N}";
        var emp = CreateEmployee($"emp_{Guid.CreateVersion7()}", TenantId, "dept-001", firebaseUid: uid);
        await _repo.SaveAsync(emp);

        // Act
        var result = await _repo.GetByFirebaseUidAsync(TenantId, uid);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.EmployeeId.Should().Be(emp.EmployeeId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ZenoHR.Module.Employee.Aggregates.Employee CreateEmployee(
        string id, string tenantId, string deptId, string? firebaseUid = null)
    {
        var uid = firebaseUid ?? $"uid_{Guid.NewGuid():N}";
        var result = ZenoHR.Module.Employee.Aggregates.Employee.Create(
            employeeId: id,
            tenantId: tenantId,
            firebaseUid: uid,
            legalName: "Test Employee",
            nationalIdOrPassport: "8001015009087",
            taxReference: "9123456789",
            dateOfBirth: new DateOnly(1980, 1, 1),
            personalPhoneNumber: "+27821234567",
            personalEmail: $"{id}@zenowethu.co.za",
            workEmail: $"work.{id}@zenowethu.co.za",
            nationality: "ZA",
            gender: "Male",
            race: "African",
            disabilityStatus: false,
            disabilityDescription: null,
            hireDate: new DateOnly(2023, 1, 1),
            employeeType: EmployeeType.Permanent,
            departmentId: deptId,
            roleId: "role-001",
            systemRole: "Employee",
            reportsToEmployeeId: null,
            actorId: "actor-001",
            now: DateTimeOffset.UtcNow);

        return result.Value!;
    }
}
