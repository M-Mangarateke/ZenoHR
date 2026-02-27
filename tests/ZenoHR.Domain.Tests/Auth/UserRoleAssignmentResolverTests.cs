// TC-SEC-001: Route authorization — effective role derived from active assignments.
// TC-SEC-009: Multi-dept Manager — team scope = union of all managed departments.
// REQ-SEC-002: 5-role model with correct privilege ordering (SaasAdmin > Director > ... > Employee).
// REQ-SEC-003: Expired/inactive assignments grant no access (SystemRole.Unknown).

using FluentAssertions;
using ZenoHR.Domain.Auth;
using ZenoHR.Domain.Common;

namespace ZenoHR.Domain.Tests.Auth;

/// <summary>
/// Unit tests for <see cref="UserRoleAssignmentResolver"/>.
/// Verifies effective role resolution (highest privilege wins) and
/// multi-department Manager scope (union of dept IDs).
/// </summary>
public sealed class UserRoleAssignmentResolverTests
{
    private static readonly DateOnly Today = new(2026, 2, 27);

    // ─── GetEffectiveSystemRole ──────────────────────────────────────────────

    [Fact]
    public void GetEffectiveSystemRole_NoAssignments_ReturnsUnknown()
    {
        // Arrange
        var assignments = Array.Empty<UserRoleAssignment>();

        // Act
        var result = UserRoleAssignmentResolver.GetEffectiveSystemRole(assignments, Today);

        // Assert
        result.Should().Be(SystemRole.Unknown);
    }

    [Fact]
    public void GetEffectiveSystemRole_SingleDirectorAssignment_ReturnsDirector()
    {
        // Arrange
        var assignments = new[] { Make(SystemRole.Director) };

        // Act
        var result = UserRoleAssignmentResolver.GetEffectiveSystemRole(assignments, Today);

        // Assert
        result.Should().Be(SystemRole.Director);
    }

    [Fact]
    public void GetEffectiveSystemRole_SingleEmployeeAssignment_ReturnsEmployee()
    {
        var result = UserRoleAssignmentResolver.GetEffectiveSystemRole(
            [Make(SystemRole.Employee)], Today);
        result.Should().Be(SystemRole.Employee);
    }

    [Fact]
    public void GetEffectiveSystemRole_DualRole_ManagerAndEmployee_ReturnsManager()
    {
        // Arrange — dual-role: Finance Manager + Warehouse Employee
        // TC-SEC-001: effective role = highest privilege = Manager
        var assignments = new[]
        {
            Make(SystemRole.Manager, deptId: "dept_finance", isPrimary: true),
            Make(SystemRole.Employee, deptId: "dept_warehouse", isPrimary: false),
        };

        // Act
        var result = UserRoleAssignmentResolver.GetEffectiveSystemRole(assignments, Today);

        // Assert
        result.Should().Be(SystemRole.Manager, because: "Manager outranks Employee");
    }

    [Fact]
    public void GetEffectiveSystemRole_SaasAdminAndDirector_ReturnsSaasAdmin()
    {
        // Hypothetical: if a user somehow had both — SaasAdmin wins (enum value 1 < 2)
        var assignments = new[]
        {
            Make(SystemRole.SaasAdmin),
            Make(SystemRole.Director),
        };

        var result = UserRoleAssignmentResolver.GetEffectiveSystemRole(assignments, Today);
        result.Should().Be(SystemRole.SaasAdmin);
    }

    [Fact]
    public void GetEffectiveSystemRole_InactiveAssignment_NotCounted()
    {
        // Arrange — revoked Director assignment
        var assignments = new[]
        {
            Make(SystemRole.Director, isActive: false),
        };

        var result = UserRoleAssignmentResolver.GetEffectiveSystemRole(assignments, Today);
        result.Should().Be(SystemRole.Unknown, because: "revoked assignments are ignored");
    }

    [Fact]
    public void GetEffectiveSystemRole_ExpiredByDate_NotCounted()
    {
        // Arrange — effective_to is yesterday
        var assignments = new[]
        {
            Make(SystemRole.HRManager, effectiveTo: Today.AddDays(-1)),
        };

        var result = UserRoleAssignmentResolver.GetEffectiveSystemRole(assignments, Today);
        result.Should().Be(SystemRole.Unknown, because: "past-effective_to assignments are ignored");
    }

    [Fact]
    public void GetEffectiveSystemRole_FutureAssignment_NotCounted()
    {
        // Arrange — effective_from is tomorrow (not yet effective)
        var assignments = new[]
        {
            Make(SystemRole.Director, effectiveFrom: Today.AddDays(1)),
        };

        var result = UserRoleAssignmentResolver.GetEffectiveSystemRole(assignments, Today);
        result.Should().Be(SystemRole.Unknown, because: "future assignments are not yet effective");
    }

    [Fact]
    public void GetEffectiveSystemRole_ExpiredPlusActiveEmployee_ReturnsEmployee()
    {
        // Arrange — expired Manager + active Employee
        var assignments = new[]
        {
            Make(SystemRole.Manager, deptId: "dept_finance", effectiveTo: Today.AddDays(-1)),
            Make(SystemRole.Employee),
        };

        var result = UserRoleAssignmentResolver.GetEffectiveSystemRole(assignments, Today);
        result.Should().Be(SystemRole.Employee);
    }

    [Fact]
    public void GetEffectiveSystemRole_AllSameRole_ReturnsThatRole()
    {
        // Arrange — two active Manager assignments (multi-dept)
        var assignments = new[]
        {
            Make(SystemRole.Manager, deptId: "dept_finance", isPrimary: true),
            Make(SystemRole.Manager, deptId: "dept_operations", isPrimary: false),
        };

        var result = UserRoleAssignmentResolver.GetEffectiveSystemRole(assignments, Today);
        result.Should().Be(SystemRole.Manager);
    }

    // ─── GetManagerDeptIds ───────────────────────────────────────────────────

    [Fact]
    public void GetManagerDeptIds_SingleManagerAssignment_ReturnsSingleDeptId()
    {
        // TC-SEC-009
        var assignments = new[] { Make(SystemRole.Manager, deptId: "dept_finance") };

        var result = UserRoleAssignmentResolver.GetManagerDeptIds(assignments, Today);

        result.Should().ContainSingle().Which.Should().Be("dept_finance");
    }

    [Fact]
    public void GetManagerDeptIds_MultiDeptManager_ReturnsUnionOfDeptIds()
    {
        // TC-SEC-009: multi-department Manager sees combined team view
        var assignments = new[]
        {
            Make(SystemRole.Manager, deptId: "dept_finance", isPrimary: true),
            Make(SystemRole.Manager, deptId: "dept_operations", isPrimary: false),
        };

        var result = UserRoleAssignmentResolver.GetManagerDeptIds(assignments, Today);

        result.Should().BeEquivalentTo(["dept_finance", "dept_operations"]);
    }

    [Fact]
    public void GetManagerDeptIds_DirectorAssignment_ReturnsEmpty()
    {
        // Director is tenant-scoped, not department-scoped
        var assignments = new[] { Make(SystemRole.Director) };

        var result = UserRoleAssignmentResolver.GetManagerDeptIds(assignments, Today);

        result.Should().BeEmpty(because: "Director is not dept-scoped — dept_id is null");
    }

    [Fact]
    public void GetManagerDeptIds_ExpiredManagerAssignment_NotIncluded()
    {
        var assignments = new[]
        {
            Make(SystemRole.Manager, deptId: "dept_finance", effectiveTo: Today.AddDays(-1)),
        };

        var result = UserRoleAssignmentResolver.GetManagerDeptIds(assignments, Today);

        result.Should().BeEmpty(because: "expired Manager assignment excluded from scope");
    }

    [Fact]
    public void GetManagerDeptIds_DuplicateDeptId_DeduplicatesResult()
    {
        // Same dept on two assignment documents (edge case)
        var assignments = new[]
        {
            Make(SystemRole.Manager, deptId: "dept_finance"),
            Make(SystemRole.Manager, deptId: "dept_finance"),
        };

        var result = UserRoleAssignmentResolver.GetManagerDeptIds(assignments, Today);

        result.Should().ContainSingle().Which.Should().Be("dept_finance");
    }

    // ─── GetEmployeeId / GetTenantId ─────────────────────────────────────────

    [Fact]
    public void GetEmployeeId_PrimaryAssignmentPresent_ReturnsPrimaryEmployeeId()
    {
        var assignments = new[]
        {
            Make(SystemRole.Manager, employeeId: "emp_002", isPrimary: false, deptId: "dept_ops"),
            Make(SystemRole.Manager, employeeId: "emp_001", isPrimary: true, deptId: "dept_finance"),
        };

        var result = UserRoleAssignmentResolver.GetEmployeeId(assignments, Today);

        result.Should().Be("emp_001", because: "primary assignment's employee_id is returned first");
    }

    [Fact]
    public void GetEmployeeId_NoActiveAssignments_ReturnsNull()
    {
        var assignments = new[] { Make(SystemRole.Employee, isActive: false) };

        var result = UserRoleAssignmentResolver.GetEmployeeId(assignments, Today);

        result.Should().BeNull();
    }

    [Fact]
    public void GetTenantId_ActiveAssignment_ReturnsTenantId()
    {
        var assignments = new[] { Make(SystemRole.HRManager, tenantId: "tenant_zeno") };

        var result = UserRoleAssignmentResolver.GetTenantId(assignments, Today);

        result.Should().Be("tenant_zeno");
    }

    // ─── UserRoleAssignment.IsCurrentlyActive ────────────────────────────────

    [Fact]
    public void IsCurrentlyActive_ActiveNoEndDate_ReturnsTrue()
    {
        var assignment = Make(SystemRole.Employee);
        assignment.IsCurrentlyActive(Today).Should().BeTrue();
    }

    [Fact]
    public void IsCurrentlyActive_EffectiveToToday_ReturnsTrue()
    {
        // Inclusive end date
        var assignment = Make(SystemRole.Employee, effectiveTo: Today);
        assignment.IsCurrentlyActive(Today).Should().BeTrue();
    }

    [Fact]
    public void IsCurrentlyActive_EffectiveToYesterday_ReturnsFalse()
    {
        var assignment = Make(SystemRole.Employee, effectiveTo: Today.AddDays(-1));
        assignment.IsCurrentlyActive(Today).Should().BeFalse();
    }

    [Fact]
    public void IsCurrentlyActive_InactiveFlag_ReturnsFalse()
    {
        var assignment = Make(SystemRole.Director, isActive: false);
        assignment.IsCurrentlyActive(Today).Should().BeFalse();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static UserRoleAssignment Make(
        SystemRole role,
        string tenantId = "tenant_test",
        string employeeId = "emp_001",
        string? deptId = null,
        bool isPrimary = true,
        bool isActive = true,
        DateOnly? effectiveFrom = null,
        DateOnly? effectiveTo = null) =>
        new()
        {
            AssignmentId = Guid.NewGuid().ToString("N"),
            TenantId = tenantId,
            FirebaseUid = "uid_test",
            EmployeeId = employeeId,
            RoleId = $"role_{role.ToString().ToLower()}",
            SystemRole = role,
            DepartmentId = deptId,
            IsPrimary = isPrimary,
            IsActive = isActive,
            EffectiveFrom = effectiveFrom ?? Today.AddDays(-30),
            EffectiveTo = effectiveTo,
        };
}
