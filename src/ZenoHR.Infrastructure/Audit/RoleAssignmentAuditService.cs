// REQ-SEC-009, CTL-SEC-008, CTL-POPIA-001: Audit logging for role assignment changes.
// VUL-014 remediation: all role create/revoke operations written to the hash-chained audit trail.
using Microsoft.Extensions.Logging;
using ZenoHR.Module.Audit.Domain;

namespace ZenoHR.Infrastructure.Audit;

/// <summary>
/// Writes role assignment audit events to the tenant's hash-chained audit trail.
/// <para>
/// Inject and call on every role assignment creation and revocation.
/// All privilege escalation and de-escalation events MUST be audited per CTL-SEC-008.
/// </para>
/// REQ-SEC-009, CTL-SEC-008
/// </summary>
public sealed partial class RoleAssignmentAuditService
{
    private readonly AuditEventWriter _auditWriter;
    private readonly ILogger<RoleAssignmentAuditService> _logger;

    public RoleAssignmentAuditService(
        AuditEventWriter auditWriter,
        ILogger<RoleAssignmentAuditService> logger)
    {
        ArgumentNullException.ThrowIfNull(auditWriter);
        ArgumentNullException.ThrowIfNull(logger);
        _auditWriter = auditWriter;
        _logger = logger;
    }

    /// <summary>
    /// Writes an audit event when a role assignment is created (privilege escalation).
    /// CTL-SEC-008: All privilege escalation events are audited.
    /// </summary>
    /// <param name="tenantId">Tenant that owns this event. Required.</param>
    /// <param name="actorId">Firebase UID of the HR Manager or Director performing the assignment.</param>
    /// <param name="actorRole">System role name of the actor (e.g., "HRManager", "Director").</param>
    /// <param name="targetUserId">Firebase UID of the user receiving the role.</param>
    /// <param name="assignmentId">Firestore document ID of the user_role_assignments document.</param>
    /// <param name="roleName">Human-readable role name (e.g., "Finance Manager").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public Task<Domain.Errors.Result<AuditEvent>> WriteRoleAssignedAsync(
        string tenantId,
        string actorId,
        string actorRole,
        string targetUserId,
        string assignmentId,
        string roleName,
        CancellationToken ct = default)
    {
        // REQ-SEC-009: Role assignment audit with structured metadata
        // CTL-POPIA-001: No PII values in metadata — field names and IDs only
        var metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            target_user_id = targetUserId,
            assignment_id = assignmentId,
            role_name = roleName,
            assigned_by = actorId
        });

        LogRoleAssigned(actorId, roleName, targetUserId, tenantId);

        return _auditWriter.WriteAsync(new WriteAuditEventRequest
        {
            TenantId = tenantId,
            ActorId = actorId,
            ActorRole = actorRole,
            Action = AuditAction.Create,
            ResourceType = AuditResourceType.UserRoleAssignment,
            ResourceId = assignmentId,
            Metadata = metadata,
            OccurredAt = DateTimeOffset.UtcNow
        }, ct);
    }

    /// <summary>
    /// Writes an audit event when a role assignment is revoked (privilege de-escalation).
    /// CTL-SEC-008: All privilege de-escalation events are audited.
    /// </summary>
    /// <param name="tenantId">Tenant that owns this event. Required.</param>
    /// <param name="actorId">Firebase UID of the HR Manager or Director revoking the assignment.</param>
    /// <param name="actorRole">System role name of the actor (e.g., "HRManager", "Director").</param>
    /// <param name="targetUserId">Firebase UID of the user losing the role.</param>
    /// <param name="assignmentId">Firestore document ID of the user_role_assignments document.</param>
    /// <param name="roleName">Human-readable role name (e.g., "Finance Manager").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public Task<Domain.Errors.Result<AuditEvent>> WriteRoleRevokedAsync(
        string tenantId,
        string actorId,
        string actorRole,
        string targetUserId,
        string assignmentId,
        string roleName,
        CancellationToken ct = default)
    {
        // REQ-SEC-009: Role revocation audit with structured metadata
        // CTL-POPIA-001: No PII values in metadata — field names and IDs only
        var metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            target_user_id = targetUserId,
            assignment_id = assignmentId,
            role_name = roleName,
            revoked_by = actorId
        });

        LogRoleRevoked(actorId, roleName, targetUserId, tenantId);

        return _auditWriter.WriteAsync(new WriteAuditEventRequest
        {
            TenantId = tenantId,
            ActorId = actorId,
            ActorRole = actorRole,
            Action = AuditAction.Delete,
            ResourceType = AuditResourceType.UserRoleAssignment,
            ResourceId = assignmentId,
            Metadata = metadata,
            OccurredAt = DateTimeOffset.UtcNow
        }, ct);
    }

    // ── Diagnostic logging (source-generated, zero-allocation) ───────────────

    [LoggerMessage(EventId = 5000, Level = LogLevel.Information,
        Message = "RoleAssignment created: actor={ActorId} assigned role '{RoleName}' to user {TargetUserId} in tenant {TenantId}")]
    private partial void LogRoleAssigned(string actorId, string roleName, string targetUserId, string tenantId);

    [LoggerMessage(EventId = 5001, Level = LogLevel.Information,
        Message = "RoleAssignment revoked: actor={ActorId} revoked role '{RoleName}' from user {TargetUserId} in tenant {TenantId}")]
    private partial void LogRoleRevoked(string actorId, string roleName, string targetUserId, string tenantId);
}
