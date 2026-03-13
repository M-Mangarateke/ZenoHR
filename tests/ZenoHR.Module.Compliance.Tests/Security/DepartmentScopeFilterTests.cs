// VUL-008: Tests for Manager department scoping enforcement at API layer.
// REQ-SEC-002: PRD-15 §1.7 — Manager queries scoped to their department(s).
// TC-SEC-020: Department scope filter validates role-based data access boundaries.

using System.Security.Claims;
using FluentAssertions;
using ZenoHR.Api.Auth;
using ZenoHR.Domain.Common;

namespace ZenoHR.Module.Compliance.Tests.Security;

/// <summary>
/// Unit tests for <see cref="DepartmentScopeFilter"/> — role-based department data filtering.
/// </summary>
public sealed class DepartmentScopeFilterTests
{
    private readonly DepartmentScopeFilter _sut = new();

    // ── Test data ─────────────────────────────────────────────────────────────

    private sealed record TestItem(string Name, string DepartmentId);

    private static readonly TestItem[] AllItems =
    [
        new("Alice", "dept_finance"),
        new("Bob", "dept_finance"),
        new("Carol", "dept_operations"),
        new("Dave", "dept_operations"),
        new("Eve", "dept_hr"),
    ];

    // ── Helper: build ClaimsPrincipal ──────────────────────────────────────────

    private static ClaimsPrincipal CreateUser(SystemRole role, params string[] deptIds)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role.ToString()),
            new(ZenoHrClaimNames.TenantId, "tenant_001"),
            new(ZenoHrClaimNames.EmployeeId, "emp_001"),
        };

        foreach (var deptId in deptIds)
        {
            claims.Add(new Claim(ZenoHrClaimNames.DeptId, deptId));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateUserWithoutRole()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ZenoHrClaimNames.TenantId, "tenant_001")],
            "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    // ── Manager: single department ────────────────────────────────────────────

    [Fact]
    public void FilterByDepartmentScope_ManagerWithSingleDept_ReturnsOnlyThatDeptData()
    {
        // Arrange
        var user = CreateUser(SystemRole.Manager, "dept_finance");

        // Act
        var result = _sut.FilterByDepartmentScope(
            AllItems, item => item.DepartmentId, user);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().OnlyContain(i => i.DepartmentId == "dept_finance");
    }

    // ── Manager: multiple departments (union) ─────────────────────────────────

    [Fact]
    public void FilterByDepartmentScope_ManagerWithMultipleDepts_ReturnsUnion()
    {
        // Arrange — PRD-15 §1.7: multi-dept Manager scope = union
        var user = CreateUser(SystemRole.Manager, "dept_finance", "dept_operations");

        // Act
        var result = _sut.FilterByDepartmentScope(
            AllItems, item => item.DepartmentId, user);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(4);
        result.Value.Should().OnlyContain(
            i => i.DepartmentId == "dept_finance" || i.DepartmentId == "dept_operations");
    }

    // ── Director: sees all ────────────────────────────────────────────────────

    [Fact]
    public void FilterByDepartmentScope_Director_ReturnsAllItems()
    {
        // Arrange
        var user = CreateUser(SystemRole.Director);

        // Act
        var result = _sut.FilterByDepartmentScope(
            AllItems, item => item.DepartmentId, user);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(AllItems.Length);
    }

    // ── HRManager: sees all ───────────────────────────────────────────────────

    [Fact]
    public void FilterByDepartmentScope_HRManager_ReturnsAllItems()
    {
        // Arrange
        var user = CreateUser(SystemRole.HRManager);

        // Act
        var result = _sut.FilterByDepartmentScope(
            AllItems, item => item.DepartmentId, user);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(AllItems.Length);
    }

    // ── Employee: gets empty ──────────────────────────────────────────────────

    [Fact]
    public void FilterByDepartmentScope_Employee_ReturnsEmpty()
    {
        // Arrange
        var user = CreateUser(SystemRole.Employee);

        // Act
        var result = _sut.FilterByDepartmentScope(
            AllItems, item => item.DepartmentId, user);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── SaasAdmin: gets empty ─────────────────────────────────────────────────

    [Fact]
    public void FilterByDepartmentScope_SaasAdmin_ReturnsEmpty()
    {
        // Arrange
        var user = CreateUser(SystemRole.SaasAdmin);

        // Act
        var result = _sut.FilterByDepartmentScope(
            AllItems, item => item.DepartmentId, user);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── Manager: no dept claims ───────────────────────────────────────────────

    [Fact]
    public void FilterByDepartmentScope_ManagerWithNoDeptClaims_ReturnsEmpty()
    {
        // Arrange — Manager with no dept_id claims
        var user = CreateUser(SystemRole.Manager);

        // Act
        var result = _sut.FilterByDepartmentScope(
            AllItems, item => item.DepartmentId, user);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── No role claim ─────────────────────────────────────────────────────────

    [Fact]
    public void FilterByDepartmentScope_NoRoleClaim_ReturnsFailure()
    {
        // Arrange
        var user = CreateUserWithoutRole();

        // Act
        var result = _sut.FilterByDepartmentScope(
            AllItems, item => item.DepartmentId, user);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(Domain.Errors.ZenoHrErrorCode.Unauthorized);
    }

    // ── Null inputs ───────────────────────────────────────────────────────────

    [Fact]
    public void FilterByDepartmentScope_NullItems_ThrowsArgumentNullException()
    {
        // Arrange
        var user = CreateUser(SystemRole.Director);

        // Act
        var act = () => _sut.FilterByDepartmentScope<TestItem>(
            null!, item => item.DepartmentId, user);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("items");
    }

    [Fact]
    public void FilterByDepartmentScope_NullDepartmentSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var user = CreateUser(SystemRole.Director);

        // Act
        var act = () => _sut.FilterByDepartmentScope(
            AllItems, (Func<TestItem, string>)null!, user);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("departmentSelector");
    }

    [Fact]
    public void FilterByDepartmentScope_NullUser_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.FilterByDepartmentScope(
            AllItems, item => item.DepartmentId, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("user");
    }

    // ── Empty input collection ────────────────────────────────────────────────

    [Fact]
    public void FilterByDepartmentScope_EmptyItems_ReturnsEmptySuccess()
    {
        // Arrange
        var user = CreateUser(SystemRole.Manager, "dept_finance");

        // Act
        var result = _sut.FilterByDepartmentScope(
            Array.Empty<TestItem>(), item => item.DepartmentId, user);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── Case-insensitive department matching ──────────────────────────────────

    [Fact]
    public void FilterByDepartmentScope_ManagerDeptIdCaseInsensitive_MatchesItems()
    {
        // Arrange — dept claim has different casing
        var user = CreateUser(SystemRole.Manager, "DEPT_FINANCE");

        // Act
        var result = _sut.FilterByDepartmentScope(
            AllItems, item => item.DepartmentId, user);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().OnlyContain(i => i.DepartmentId == "dept_finance");
    }

    // ── Manager accessing dept with no matching items ─────────────────────────

    [Fact]
    public void FilterByDepartmentScope_ManagerWithDeptButNoMatchingItems_ReturnsEmpty()
    {
        // Arrange
        var user = CreateUser(SystemRole.Manager, "dept_nonexistent");

        // Act
        var result = _sut.FilterByDepartmentScope(
            AllItems, item => item.DepartmentId, user);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
