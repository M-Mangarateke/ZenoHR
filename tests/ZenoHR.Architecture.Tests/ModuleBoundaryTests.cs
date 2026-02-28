// TC-ARC-001: Module boundary enforcement — no direct cross-module dependencies.
// REQ-OPS-002: Modules communicate ONLY via MediatR domain events or shared kernel types.
// Uses NetArchTest.Rules for ArchUnit-style boundary enforcement.
using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;

namespace ZenoHR.Architecture.Tests;

/// <summary>
/// Enforces that no module directly references another module's internals.
/// All cross-module communication must go through MediatR events or the shared Domain kernel.
/// </summary>
public sealed class ModuleBoundaryTests
{
    // ── Module namespace roots ─────────────────────────────────────────────────

    private const string EmployeeNs = "ZenoHR.Module.Employee";
    private const string LeaveNs = "ZenoHR.Module.Leave";
    private const string PayrollNs = "ZenoHR.Module.Payroll";
    private const string ComplianceNs = "ZenoHR.Module.Compliance";
    private const string DomainNs = "ZenoHR.Domain";

    // ── Assembly loader helper ────────────────────────────────────────────────

    /// <summary>
    /// Loads an assembly by namespace prefix. Searches loaded assemblies first,
    /// then falls back to disk relative to the test assembly output directory.
    /// </summary>
    private static Assembly LoadModule(string namePrefix)
    {
        // Search already-loaded assemblies first
        var existing = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name?.Equals(namePrefix, StringComparison.Ordinal) == true);
        if (existing is not null) return existing;

        // Fall back to loading from disk
        var testDir = Path.GetDirectoryName(typeof(ModuleBoundaryTests).Assembly.Location)!;
        var path = Path.Combine(testDir, namePrefix + ".dll");
        return Assembly.LoadFrom(path);
    }

    // ── Employee module isolation ─────────────────────────────────────────────

    [Fact]
    public void Employee_MustNotDirectlyDependOn_Payroll()
    {
        // TC-ARC-001-001
        var result = Types.InAssembly(LoadModule("ZenoHR.Module.Employee"))
            .That().ResideInNamespaceStartingWith(EmployeeNs)
            .ShouldNot().HaveDependencyOn(PayrollNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Employee module must not directly reference Payroll module types. Use domain events.");
    }

    [Fact]
    public void Employee_MustNotDirectlyDependOn_Leave()
    {
        // TC-ARC-001-002
        var result = Types.InAssembly(LoadModule("ZenoHR.Module.Employee"))
            .That().ResideInNamespaceStartingWith(EmployeeNs)
            .ShouldNot().HaveDependencyOn(LeaveNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Employee module must not directly reference Leave module types.");
    }

    [Fact]
    public void Employee_MustNotDirectlyDependOn_Compliance()
    {
        // TC-ARC-001-003
        var result = Types.InAssembly(LoadModule("ZenoHR.Module.Employee"))
            .That().ResideInNamespaceStartingWith(EmployeeNs)
            .ShouldNot().HaveDependencyOn(ComplianceNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Employee module must not directly reference Compliance module types.");
    }

    // ── Payroll module isolation ──────────────────────────────────────────────

    [Fact]
    public void Payroll_MustNotDirectlyDependOn_Employee()
    {
        // TC-ARC-001-004
        var result = Types.InAssembly(LoadModule("ZenoHR.Module.Payroll"))
            .That().ResideInNamespaceStartingWith(PayrollNs)
            .ShouldNot().HaveDependencyOn(EmployeeNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Payroll module must not directly reference Employee module types.");
    }

    [Fact]
    public void Payroll_MustNotDirectlyDependOn_Leave()
    {
        // TC-ARC-001-005
        var result = Types.InAssembly(LoadModule("ZenoHR.Module.Payroll"))
            .That().ResideInNamespaceStartingWith(PayrollNs)
            .ShouldNot().HaveDependencyOn(LeaveNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Payroll module must not directly reference Leave module types.");
    }

    [Fact]
    public void Payroll_MustNotDirectlyDependOn_Compliance()
    {
        // TC-ARC-001-006
        var result = Types.InAssembly(LoadModule("ZenoHR.Module.Payroll"))
            .That().ResideInNamespaceStartingWith(PayrollNs)
            .ShouldNot().HaveDependencyOn(ComplianceNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Payroll module must not directly reference Compliance module types.");
    }

    // ── Leave module isolation ────────────────────────────────────────────────

    [Fact]
    public void Leave_MustNotDirectlyDependOn_Payroll()
    {
        // TC-ARC-001-007
        var result = Types.InAssembly(LoadModule("ZenoHR.Module.Leave"))
            .That().ResideInNamespaceStartingWith(LeaveNs)
            .ShouldNot().HaveDependencyOn(PayrollNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Leave module must not directly reference Payroll module types.");
    }

    [Fact]
    public void Leave_MustNotDirectlyDependOn_Employee()
    {
        // TC-ARC-001-008
        var result = Types.InAssembly(LoadModule("ZenoHR.Module.Leave"))
            .That().ResideInNamespaceStartingWith(LeaveNs)
            .ShouldNot().HaveDependencyOn(EmployeeNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Leave module must not directly reference Employee module types.");
    }

    // ── Compliance module isolation ───────────────────────────────────────────

    [Fact]
    public void Compliance_MustNotDirectlyDependOn_Employee()
    {
        // TC-ARC-001-009
        var result = Types.InAssembly(LoadModule("ZenoHR.Module.Compliance"))
            .That().ResideInNamespaceStartingWith(ComplianceNs)
            .ShouldNot().HaveDependencyOn(EmployeeNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Compliance module must not directly reference Employee module types.");
    }

    [Fact]
    public void Compliance_MustNotDirectlyDependOn_Leave()
    {
        // TC-ARC-001-010
        var result = Types.InAssembly(LoadModule("ZenoHR.Module.Compliance"))
            .That().ResideInNamespaceStartingWith(ComplianceNs)
            .ShouldNot().HaveDependencyOn(LeaveNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Compliance module must not directly reference Leave module types.");
    }

    // ── Domain kernel: no dependencies on any module ──────────────────────────

    [Fact]
    public void Domain_MustNotDependOn_Employee()
    {
        // TC-ARC-001-011a
        var result = Types.InAssembly(LoadModule("ZenoHR.Domain"))
            .That().ResideInNamespaceStartingWith(DomainNs)
            .ShouldNot().HaveDependencyOn(EmployeeNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue("Domain kernel must not reference Employee module.");
    }

    [Fact]
    public void Domain_MustNotDependOn_Payroll()
    {
        // TC-ARC-001-011b
        var result = Types.InAssembly(LoadModule("ZenoHR.Domain"))
            .That().ResideInNamespaceStartingWith(DomainNs)
            .ShouldNot().HaveDependencyOn(PayrollNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue("Domain kernel must not reference Payroll module.");
    }

    [Fact]
    public void Domain_MustNotDependOn_Leave()
    {
        // TC-ARC-001-011c
        var result = Types.InAssembly(LoadModule("ZenoHR.Domain"))
            .That().ResideInNamespaceStartingWith(DomainNs)
            .ShouldNot().HaveDependencyOn(LeaveNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue("Domain kernel must not reference Leave module.");
    }
}
