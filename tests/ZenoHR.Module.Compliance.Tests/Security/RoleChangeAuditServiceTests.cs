// VUL-014: Tests for role assignment audit logging.
// REQ-SEC-002: Verifies that role changes are correctly recorded in audit records.

using System.Text.Json;
using FluentAssertions;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Audit.Models;
using ZenoHR.Module.Audit.Services;

namespace ZenoHR.Module.Compliance.Tests.Security;

public sealed class RoleChangeAuditServiceTests
{
    private readonly DateTimeOffset _timestamp = new(2026, 3, 13, 10, 0, 0, TimeSpan.Zero);

    // ── LogRoleAssigned ────────────────────────────────────────────────────

    [Fact]
    public void LogRoleAssigned_ValidInputs_CreatesCorrectRecord()
    {
        var result = RoleChangeAuditService.LogRoleAssigned(
            "tenant-001", "emp-001", "HRManager", "dept-fin", "admin-001", _timestamp);

        result.IsSuccess.Should().BeTrue();
        var record = result.Value;
        record.TenantId.Should().Be("tenant-001");
        record.EmployeeId.Should().Be("emp-001");
        record.RoleName.Should().Be("HRManager");
        record.DepartmentId.Should().Be("dept-fin");
        record.PerformedBy.Should().Be("admin-001");
        record.Timestamp.Should().Be(_timestamp);
        record.Action.Should().Be(RoleChangeAction.Assigned);
        record.RecordId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void LogRoleAssigned_Metadata_IsValidJson()
    {
        var result = RoleChangeAuditService.LogRoleAssigned(
            "tenant-001", "emp-001", "Manager", "dept-ops", "admin-001", _timestamp);

        result.IsSuccess.Should().BeTrue();
        var act = () => JsonDocument.Parse(result.Value.Metadata);
        act.Should().NotThrow("metadata must be valid JSON");
    }

    [Fact]
    public void LogRoleAssigned_Action_IsAssigned()
    {
        var result = RoleChangeAuditService.LogRoleAssigned(
            "tenant-001", "emp-001", "Director", "dept-001", "admin-001", _timestamp);

        result.IsSuccess.Should().BeTrue();
        result.Value.Action.Should().Be(RoleChangeAction.Assigned);
    }

    // ── LogRoleRevoked ─────────────────────────────────────────────────────

    [Fact]
    public void LogRoleRevoked_ValidInputs_CreatesCorrectRecordWithReason()
    {
        var result = RoleChangeAuditService.LogRoleRevoked(
            "tenant-001", "emp-002", "Manager", "dept-hr", "admin-001", "Employee resigned", _timestamp);

        result.IsSuccess.Should().BeTrue();
        var record = result.Value;
        record.TenantId.Should().Be("tenant-001");
        record.EmployeeId.Should().Be("emp-002");
        record.RoleName.Should().Be("Manager");
        record.DepartmentId.Should().Be("dept-hr");
        record.PerformedBy.Should().Be("admin-001");
        record.Action.Should().Be(RoleChangeAction.Revoked);
        record.Metadata.Should().Contain("Employee resigned");
    }

    [Fact]
    public void LogRoleRevoked_Metadata_ContainsReasonInValidJson()
    {
        var result = RoleChangeAuditService.LogRoleRevoked(
            "tenant-001", "emp-002", "Manager", "dept-hr", "admin-001", "Transferred to another dept", _timestamp);

        result.IsSuccess.Should().BeTrue();
        var doc = JsonDocument.Parse(result.Value.Metadata);
        doc.RootElement.GetProperty("reason").GetString().Should().Be("Transferred to another dept");
    }

    [Fact]
    public void LogRoleRevoked_Action_IsRevoked()
    {
        var result = RoleChangeAuditService.LogRoleRevoked(
            "tenant-001", "emp-002", "Manager", "dept-hr", "admin-001", "No longer needed", _timestamp);

        result.IsSuccess.Should().BeTrue();
        result.Value.Action.Should().Be(RoleChangeAction.Revoked);
    }

    // ── LogRoleModified ────────────────────────────────────────────────────

    [Fact]
    public void LogRoleModified_ValidInputs_CreatesCorrectRecordWithChanges()
    {
        var result = RoleChangeAuditService.LogRoleModified(
            "tenant-001", "role-custom-fin", "added: leave_approve; removed: timesheet_view", "admin-001", _timestamp);

        result.IsSuccess.Should().BeTrue();
        var record = result.Value;
        record.TenantId.Should().Be("tenant-001");
        record.EmployeeId.Should().Be("role-custom-fin");
        record.RoleName.Should().Be("role-custom-fin");
        record.PerformedBy.Should().Be("admin-001");
        record.Action.Should().Be(RoleChangeAction.Modified);
        record.Metadata.Should().Contain("added: leave_approve; removed: timesheet_view");
    }

    [Fact]
    public void LogRoleModified_Metadata_ContainsChangesInValidJson()
    {
        var result = RoleChangeAuditService.LogRoleModified(
            "tenant-001", "role-custom-fin", "added: employee_view", "admin-001", _timestamp);

        result.IsSuccess.Should().BeTrue();
        var doc = JsonDocument.Parse(result.Value.Metadata);
        doc.RootElement.GetProperty("changes").GetString().Should().Be("added: employee_view");
    }

    [Fact]
    public void LogRoleModified_Action_IsModified()
    {
        var result = RoleChangeAuditService.LogRoleModified(
            "tenant-001", "role-custom-ops", "removed: leave_approve", "admin-001", _timestamp);

        result.IsSuccess.Should().BeTrue();
        result.Value.Action.Should().Be(RoleChangeAction.Modified);
    }

    // ── Validation — Empty/Null fields rejected ────────────────────────────

    [Theory]
    [InlineData("", "emp-001", "Manager", "admin-001", "tenantId")]
    [InlineData("  ", "emp-001", "Manager", "admin-001", "tenantId")]
    [InlineData("tenant-001", "", "Manager", "admin-001", "employeeId")]
    [InlineData("tenant-001", "  ", "Manager", "admin-001", "employeeId")]
    [InlineData("tenant-001", "emp-001", "", "admin-001", "roleName")]
    [InlineData("tenant-001", "emp-001", "  ", "admin-001", "roleName")]
    [InlineData("tenant-001", "emp-001", "Manager", "", "performedBy")]
    [InlineData("tenant-001", "emp-001", "Manager", "  ", "performedBy")]
    public void LogRoleAssigned_EmptyRequiredField_ReturnsFailure(
        string tenantId, string employeeId, string roleName, string performedBy, string expectedField)
    {
        var result = RoleChangeAuditService.LogRoleAssigned(tenantId, employeeId, roleName, "dept-001", performedBy, _timestamp);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
        result.Error.PropertyName.Should().Be(expectedField);
    }

    [Theory]
    [InlineData("", "emp-001", "Manager", "admin-001")]
    [InlineData("tenant-001", "", "Manager", "admin-001")]
    [InlineData("tenant-001", "emp-001", "", "admin-001")]
    [InlineData("tenant-001", "emp-001", "Manager", "")]
    public void LogRoleRevoked_EmptyRequiredField_ReturnsFailure(
        string tenantId, string employeeId, string roleName, string performedBy)
    {
        var result = RoleChangeAuditService.LogRoleRevoked(tenantId, employeeId, roleName, "dept-001", performedBy, "reason", _timestamp);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    [Theory]
    [InlineData("", "role-001", "admin-001")]
    [InlineData("tenant-001", "", "admin-001")]
    [InlineData("tenant-001", "role-001", "")]
    public void LogRoleModified_EmptyRequiredField_ReturnsFailure(
        string tenantId, string roleId, string modifiedBy)
    {
        var result = RoleChangeAuditService.LogRoleModified(tenantId, roleId, "some changes", modifiedBy, _timestamp);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    // ── Record ID uniqueness ───────────────────────────────────────────────

    [Fact]
    public void LogRoleAssigned_MultipleCalls_ProducesUniqueRecordIds()
    {
        var result1 = RoleChangeAuditService.LogRoleAssigned("tenant-001", "emp-001", "Manager", "dept-001", "admin-001", _timestamp);
        var result2 = RoleChangeAuditService.LogRoleAssigned("tenant-001", "emp-001", "Manager", "dept-001", "admin-001", _timestamp);

        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result1.Value.RecordId.Should().NotBe(result2.Value.RecordId);
    }

    [Fact]
    public void LogRoleRevoked_MultipleCalls_ProducesUniqueRecordIds()
    {
        var result1 = RoleChangeAuditService.LogRoleRevoked("tenant-001", "emp-001", "Manager", "dept-001", "admin-001", "reason", _timestamp);
        var result2 = RoleChangeAuditService.LogRoleRevoked("tenant-001", "emp-001", "Manager", "dept-001", "admin-001", "reason", _timestamp);

        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result1.Value.RecordId.Should().NotBe(result2.Value.RecordId);
    }

    // ── Metadata is valid JSON for all action types ────────────────────────

    [Fact]
    public void AllMethods_Metadata_IsValidJson()
    {
        var assigned = RoleChangeAuditService.LogRoleAssigned("t", "e", "r", "d", "p", _timestamp);
        var revoked = RoleChangeAuditService.LogRoleRevoked("t", "e", "r", "d", "p", "reason", _timestamp);
        var modified = RoleChangeAuditService.LogRoleModified("t", "r", "changes", "p", _timestamp);

        assigned.IsSuccess.Should().BeTrue();
        revoked.IsSuccess.Should().BeTrue();
        modified.IsSuccess.Should().BeTrue();

        var act1 = () => JsonDocument.Parse(assigned.Value.Metadata);
        var act2 = () => JsonDocument.Parse(revoked.Value.Metadata);
        var act3 = () => JsonDocument.Parse(modified.Value.Metadata);

        act1.Should().NotThrow();
        act2.Should().NotThrow();
        act3.Should().NotThrow();
    }
}
