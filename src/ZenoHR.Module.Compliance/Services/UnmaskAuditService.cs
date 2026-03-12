// CTL-POPIA-002, VUL-020: Records audit trail for every PII unmask operation.
// REQ-SEC-001: Purpose code is mandatory and logged in metadata.
// Module boundary: returns UnmaskAuditRecord — AuditEvent creation delegated to infrastructure.

using System.Globalization;
using System.Text.Json;

namespace ZenoHR.Module.Compliance.Services;

/// <summary>
/// Creates audit metadata for PII unmask operations.
/// CTL-POPIA-002: Purpose limitation — every unmask must state WHY access is needed.
/// VUL-020: Closes the unmask-without-purpose vulnerability.
/// <para>
/// Module boundary note: This service produces an <see cref="UnmaskAuditRecord"/>
/// that the API/Infrastructure layer maps to an <c>AuditEvent</c>. Direct dependency
/// on ZenoHR.Module.Audit is prohibited (module isolation rule).
/// </para>
/// </summary>
public sealed class UnmaskAuditService
{
    /// <summary>
    /// Creates an <see cref="UnmaskAuditRecord"/> capturing who unmasked which field,
    /// for which employee, and the POPIA-approved purpose code justifying the access.
    /// </summary>
    /// <param name="tenantId">Tenant owning the employee record.</param>
    /// <param name="actorId">Firebase UID of the user requesting unmask.</param>
    /// <param name="actorRole">System role of the actor at time of unmask.</param>
    /// <param name="employeeId">Firestore document ID of the employee whose field is unmasked.</param>
    /// <param name="fieldName">The PII field being unmasked (national_id, tax_reference, bank_account).</param>
    /// <param name="purposeCode">POPIA-approved purpose code from <c>UnmaskRequest.ApprovedPurposeCodes</c>.</param>
    /// <param name="justification">Optional free-text justification (required for AUDIT_REVIEW and HR_INVESTIGATION).</param>
    /// <param name="occurredAt">UTC timestamp of the unmask operation.</param>
    /// <returns>An <see cref="UnmaskAuditRecord"/> for downstream audit event creation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance method for DI compatibility")]
    public UnmaskAuditRecord CreateUnmaskAuditRecord(
        string tenantId,
        string actorId,
        string actorRole,
        string employeeId,
        string fieldName,
        string purposeCode,
        string? justification,
        DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorRole);
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(purposeCode);

        // CTL-POPIA-002: Metadata records the purpose code and field name — never the PII value itself.
        var metadata = JsonSerializer.Serialize(new
        {
            unmask_field = fieldName,
            purpose_code = purposeCode,
            justification = justification,
            occurred_at_iso = occurredAt.ToString("o", CultureInfo.InvariantCulture),
        });

        return new UnmaskAuditRecord(
            TenantId: tenantId,
            ActorId: actorId,
            ActorRole: actorRole,
            EmployeeId: employeeId,
            FieldName: fieldName,
            PurposeCode: purposeCode,
            Metadata: metadata,
            OccurredAt: occurredAt);
    }
}

/// <summary>
/// Immutable record capturing unmask audit data for downstream AuditEvent creation.
/// CTL-POPIA-002: Purpose code and field name are always present.
/// </summary>
public sealed record UnmaskAuditRecord(
    string TenantId,
    string ActorId,
    string ActorRole,
    string EmployeeId,
    string FieldName,
    string PurposeCode,
    string Metadata,
    DateTimeOffset OccurredAt);
