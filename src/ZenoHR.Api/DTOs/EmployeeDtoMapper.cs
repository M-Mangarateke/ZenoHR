// VUL-009 remediation: maps Employee aggregate to role-appropriate response DTO.
// REQ-SEC-001, CTL-POPIA-005
using ZenoHR.Module.Employee.Aggregates;

namespace ZenoHR.Api.DTOs;

/// <summary>
/// Maps an <see cref="Employee"/> aggregate to the appropriate role-scoped response DTO.
/// VUL-009: prevents data leakage by ensuring Managers and Employees never receive
/// salary, tax, banking, or national ID fields in API responses.
/// </summary>
public static class EmployeeDtoMapper
{
    // REQ-SEC-001: Return different DTO shapes based on the calling user's system role.
    public static object ToRoleDto(Employee employee, string systemRole) =>
        systemRole switch
        {
            "Director" or "HRManager" => ToFullDto(employee),
            "Manager" => ToProfileDto(employee),
            _ => ToSelfDto(employee) // Employee or unknown
        };

    public static EmployeeFullDto ToFullDto(Employee employee) => new()
    {
        EmployeeId = employee.EmployeeId,
        TenantId = employee.TenantId,
        LegalName = employee.LegalName,
        Department = employee.DepartmentId,
        SystemRole = employee.SystemRole,
        HireDate = employee.HireDate,
        PersonalEmail = employee.PersonalEmail,
        WorkEmail = employee.WorkEmail ?? employee.PersonalEmail,
        PhoneNumber = employee.PersonalPhoneNumber,
        NationalIdOrPassport = MaskNationalId(employee.NationalIdOrPassport),
        TaxReference = employee.TaxReference is not null
            ? MaskTaxRef(employee.TaxReference)
            : "****",
        BankAccountRef = null, // Separate endpoint with purpose code (VUL-020)
        Nationality = employee.Nationality,
        Gender = employee.Gender,
        Race = employee.Race,
    };

    public static EmployeeProfileDto ToProfileDto(Employee employee) => new()
    {
        EmployeeId = employee.EmployeeId,
        LegalName = employee.LegalName,
        Department = employee.DepartmentId,
        SystemRole = employee.SystemRole,
        HireDate = employee.HireDate,
        WorkEmail = employee.WorkEmail ?? employee.PersonalEmail,
        PhoneNumber = employee.PersonalPhoneNumber,
    };

    public static EmployeeSelfDto ToSelfDto(Employee employee) => new()
    {
        EmployeeId = employee.EmployeeId,
        LegalName = employee.LegalName,
        Department = employee.DepartmentId,
        WorkEmail = employee.WorkEmail ?? employee.PersonalEmail,
        HireDate = employee.HireDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
    };

    // CTL-POPIA-002: Mask national ID — show first 6 + last 1, mask 6 middle digits.
    // SA ID format: YYMMDD SSSS C A Z (13 digits). Mask: "800101****082"
    private static string MaskNationalId(string id) =>
        id.Length >= 13
            ? $"{id[..6]}****{id[^1]}"
            : "****";

    // CTL-POPIA-002: Mask tax reference — show last 4 digits only.
    private static string MaskTaxRef(string taxRef) =>
        taxRef.Length >= 4
            ? $"****{taxRef[^4..]}"
            : "****";
}
