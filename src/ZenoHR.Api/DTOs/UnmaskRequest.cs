// CTL-POPIA-002, REQ-SEC-001: Purpose code required for PII unmasking.
// VUL-020 remediation.
namespace ZenoHR.Api.DTOs;

/// <summary>
/// Request to unmask a sensitive PII field (national ID, tax ref, bank account).
/// CTL-POPIA-002: Purpose code is mandatory and is logged in the audit trail.
/// VUL-020: Enforces that all PII unmask operations provide an approved purpose code.
/// </summary>
public sealed record UnmaskRequest
{
    /// <summary>POPIA-approved purpose codes for PII access.</summary>
    public static readonly IReadOnlySet<string> ApprovedPurposeCodes = new HashSet<string>
    {
        "PAYROLL_PROCESSING",    // PAYE calculation requires tax reference
        "SARS_FILING",           // EMP201/EMP501 submission requires employee IDs
        "BCEA_COMPLIANCE",       // Leave compliance requires employee records
        "HR_INVESTIGATION",      // Disciplinary/grievance process
        "AUDIT_REVIEW",          // Internal audit access
        "EMPLOYEE_REQUEST",      // Employee's own right of access (POPIA §23)
        "SYSTEM_ADMIN",          // SaasAdmin technical support (restricted)
    };

    /// <summary>The field being unmasked: "national_id", "tax_reference", or "bank_account".</summary>
    public required string FieldName { get; init; }

    /// <summary>POPIA-approved purpose code from <see cref="ApprovedPurposeCodes"/>.</summary>
    public required string PurposeCode { get; init; }

    /// <summary>Optional free-text justification (required for AUDIT_REVIEW and HR_INVESTIGATION).</summary>
    public string? Justification { get; init; }

    // REQ-SEC-001: Validate purpose code and field name before allowing unmask.
    public bool IsValid() =>
        ApprovedPurposeCodes.Contains(PurposeCode)
        && !string.IsNullOrWhiteSpace(FieldName)
        && FieldName is "national_id" or "tax_reference" or "bank_account";
}
