// VUL-014: RoleChangeAuditService — creates audit records for role assignment changes.
// REQ-SEC-002: Privilege escalation detection requires logging all role assignments,
// revocations, and permission modifications in the hash-chained audit trail.

using System.Globalization;
using System.Text.Json;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Audit.Models;

namespace ZenoHR.Module.Audit.Services;

/// <summary>
/// Service responsible for creating audit records when roles are assigned, revoked, or modified.
/// All methods validate required fields and return <see cref="Result{T}"/> to signal validation failures
/// without exceptions.
/// </summary>
public sealed class RoleChangeAuditService
{
    // VUL-014: Fixed JSON options for metadata serialization — consistent, compact output.
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
    };

    /// <summary>
    /// Logs a role assignment event for an employee.
    /// </summary>
    /// <param name="tenantId">Tenant that owns this record.</param>
    /// <param name="employeeId">Employee receiving the role.</param>
    /// <param name="roleName">Name of the role being assigned.</param>
    /// <param name="departmentId">Department scope of the assignment.</param>
    /// <param name="assignedBy">Firebase UID of the actor performing the assignment.</param>
    /// <param name="timestamp">UTC timestamp of the assignment.</param>
    /// <returns>A <see cref="Result{T}"/> containing the audit record, or a validation error.</returns>
    public static Result<RoleChangeAuditRecord> LogRoleAssigned(
        string tenantId,
        string employeeId,
        string roleName,
        string departmentId,
        string assignedBy,
        DateTimeOffset timestamp)
    {
        // VUL-014: Validate all required fields before creating the record.
        var validationResult = ValidateRequiredFields(tenantId, employeeId, roleName, assignedBy);
        if (validationResult is not null)
            return Result<RoleChangeAuditRecord>.Failure(validationResult);

        var metadata = JsonSerializer.Serialize(new
        {
            action_detail = "role_assigned",
            assigned_by = assignedBy,
            timestamp = timestamp.ToString("o", CultureInfo.InvariantCulture),
        }, _jsonOptions);

        var record = new RoleChangeAuditRecord
        {
            RecordId = Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture),
            TenantId = tenantId,
            Action = RoleChangeAction.Assigned,
            EmployeeId = employeeId,
            RoleName = roleName,
            DepartmentId = departmentId,
            PerformedBy = assignedBy,
            Timestamp = timestamp,
            Metadata = metadata,
        };

        return Result<RoleChangeAuditRecord>.Success(record);
    }

    /// <summary>
    /// Logs a role revocation event for an employee.
    /// </summary>
    /// <param name="tenantId">Tenant that owns this record.</param>
    /// <param name="employeeId">Employee losing the role.</param>
    /// <param name="roleName">Name of the role being revoked.</param>
    /// <param name="departmentId">Department scope of the revocation.</param>
    /// <param name="revokedBy">Firebase UID of the actor performing the revocation.</param>
    /// <param name="reason">Business reason for the revocation.</param>
    /// <param name="timestamp">UTC timestamp of the revocation.</param>
    /// <returns>A <see cref="Result{T}"/> containing the audit record, or a validation error.</returns>
    public static Result<RoleChangeAuditRecord> LogRoleRevoked(
        string tenantId,
        string employeeId,
        string roleName,
        string departmentId,
        string revokedBy,
        string reason,
        DateTimeOffset timestamp)
    {
        // VUL-014: Validate all required fields before creating the record.
        var validationResult = ValidateRequiredFields(tenantId, employeeId, roleName, revokedBy);
        if (validationResult is not null)
            return Result<RoleChangeAuditRecord>.Failure(validationResult);

        var metadata = JsonSerializer.Serialize(new
        {
            action_detail = "role_revoked",
            revoked_by = revokedBy,
            reason = reason ?? string.Empty,
            timestamp = timestamp.ToString("o", CultureInfo.InvariantCulture),
        }, _jsonOptions);

        var record = new RoleChangeAuditRecord
        {
            RecordId = Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture),
            TenantId = tenantId,
            Action = RoleChangeAction.Revoked,
            EmployeeId = employeeId,
            RoleName = roleName,
            DepartmentId = departmentId,
            PerformedBy = revokedBy,
            Timestamp = timestamp,
            Metadata = metadata,
        };

        return Result<RoleChangeAuditRecord>.Success(record);
    }

    /// <summary>
    /// Logs a custom role permission modification event.
    /// </summary>
    /// <param name="tenantId">Tenant that owns this record.</param>
    /// <param name="roleId">Identifier of the custom role being modified.</param>
    /// <param name="changes">Description of permission changes (e.g., "added: leave_approve; removed: timesheet_view").</param>
    /// <param name="modifiedBy">Firebase UID of the actor performing the modification.</param>
    /// <param name="timestamp">UTC timestamp of the modification.</param>
    /// <returns>A <see cref="Result{T}"/> containing the audit record, or a validation error.</returns>
    public static Result<RoleChangeAuditRecord> LogRoleModified(
        string tenantId,
        string roleId,
        string changes,
        string modifiedBy,
        DateTimeOffset timestamp)
    {
        // VUL-014: Validate all required fields before creating the record.
        // roleId serves as the roleName for modified actions.
        var validationResult = ValidateRequiredFields(tenantId, roleId, roleId, modifiedBy);
        if (validationResult is not null)
            return Result<RoleChangeAuditRecord>.Failure(validationResult);

        var metadata = JsonSerializer.Serialize(new
        {
            action_detail = "role_modified",
            modified_by = modifiedBy,
            changes = changes ?? string.Empty,
            timestamp = timestamp.ToString("o", CultureInfo.InvariantCulture),
        }, _jsonOptions);

        var record = new RoleChangeAuditRecord
        {
            RecordId = Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture),
            TenantId = tenantId,
            Action = RoleChangeAction.Modified,
            EmployeeId = roleId,
            RoleName = roleId,
            DepartmentId = string.Empty,
            PerformedBy = modifiedBy,
            Timestamp = timestamp,
            Metadata = metadata,
        };

        return Result<RoleChangeAuditRecord>.Success(record);
    }

    // ── Validation ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates that required string fields are not null, empty, or whitespace.
    /// Returns a <see cref="ZenoHrError"/> if any field is invalid, or <c>null</c> if all pass.
    /// </summary>
    private static ZenoHrError? ValidateRequiredFields(
        string tenantId,
        string employeeId,
        string roleName,
        string performedBy)
    {
        // VUL-014: All fields are mandatory — missing any prevents audit record creation.
        if (string.IsNullOrWhiteSpace(tenantId))
            return new ZenoHrError(ZenoHrErrorCode.RequiredFieldMissing, "TenantId is required.", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(employeeId))
            return new ZenoHrError(ZenoHrErrorCode.RequiredFieldMissing, "EmployeeId is required.", nameof(employeeId));

        if (string.IsNullOrWhiteSpace(roleName))
            return new ZenoHrError(ZenoHrErrorCode.RequiredFieldMissing, "RoleName is required.", nameof(roleName));

        if (string.IsNullOrWhiteSpace(performedBy))
            return new ZenoHrError(ZenoHrErrorCode.RequiredFieldMissing, "PerformedBy is required.", nameof(performedBy));

        return null;
    }
}
