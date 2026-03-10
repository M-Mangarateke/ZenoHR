// REQ-SEC-001, CTL-POPIA-002: Role-scoped employee response DTOs.
// VUL-009 remediation: different response shapes per role to prevent data leakage.
// Manager/Employee see only profile fields. Director/HRManager see full record.
namespace ZenoHR.Api.DTOs;

/// <summary>
/// Full employee record for Director and HRManager roles.
/// Includes salary, tax reference, bank account reference, and all PII.
/// CTL-POPIA-002: Access logged when sensitive fields are included.
/// </summary>
public sealed record EmployeeFullDto
{
    public required string EmployeeId { get; init; }
    public required string TenantId { get; init; }
    public required string LegalName { get; init; }
    public required string Department { get; init; }
    public required string SystemRole { get; init; }
    public required DateOnly HireDate { get; init; }
    public required string PersonalEmail { get; init; }
    public required string WorkEmail { get; init; }
    public required string PhoneNumber { get; init; }
    // Sensitive fields — HR/Director only
    public required string NationalIdOrPassport { get; init; }  // masked: "800101****082"
    public required string TaxReference { get; init; }           // masked: "****6789"
    public string? BankAccountRef { get; init; }                 // masked or ref ID only
    public required string Nationality { get; init; }
    public required string Gender { get; init; }
    public required string Race { get; init; }                   // EEA reporting
}

/// <summary>
/// Restricted employee profile for Manager role.
/// No salary, no tax, no banking, no national ID.
/// REQ-SEC-001: Managers see only profile and contact details for their team.
/// </summary>
public sealed record EmployeeProfileDto
{
    public required string EmployeeId { get; init; }
    public required string LegalName { get; init; }
    public required string Department { get; init; }
    public required string SystemRole { get; init; }
    public required DateOnly HireDate { get; init; }
    public required string WorkEmail { get; init; }
    public required string PhoneNumber { get; init; }
    // No: NationalId, TaxRef, BankAccount, Salary, Nationality, Race
}

/// <summary>
/// Self-service view for Employee role.
/// Can see own profile and contact; no sensitive PII details exposed.
/// </summary>
public sealed record EmployeeSelfDto
{
    public required string EmployeeId { get; init; }
    public required string LegalName { get; init; }
    public required string Department { get; init; }
    public required string WorkEmail { get; init; }
    public required string HireDate { get; init; }
}
