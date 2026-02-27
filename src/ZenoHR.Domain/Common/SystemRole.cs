// REQ-SEC-002: 5-role access control model for ZenoHR.
// Enum values are defined in PRD-15 Section 3 — do not change without a PRD amendment.
// Lower integer value = higher privilege (SaasAdmin=1 is highest; Employee=5 is lowest).

namespace ZenoHR.Domain.Common;

/// <summary>
/// The five system roles that govern access in ZenoHR.
/// Defined in PRD-15 Section 2 and 3. Values are stable — do not renumber.
/// </summary>
/// <remarks>
/// Priority order (highest to lowest): SaasAdmin → Director → HRManager → Manager → Employee.
/// When a user holds multiple role assignments the effective role is the one with the lowest
/// integer value (highest privilege). See PRD-15 Section 1.7.
/// </remarks>
public enum SystemRole
{
    /// <summary>No role assigned or unknown. User is denied all access.</summary>
    Unknown = 0,

    /// <summary>
    /// Platform operator — cross-tenant. Accesses only <c>/admin/*</c> UI.
    /// Has no employee record, no payslip, no leave. Cannot read tenant data.
    /// </summary>
    SaasAdmin = 1,

    /// <summary>
    /// Tenant superuser (CEO/MD/Owner). Full access to all tenant screens.
    /// Creates roles, departments, and manages users.
    /// Also an employee of the <em>Executive</em> system department.
    /// </summary>
    Director = 2,

    /// <summary>
    /// Tenant admin. Identical system access to <see cref="Director"/>.
    /// Head of HR — day-to-day operator of ZenoHR.
    /// Also an employee of the <em>Executive</em> system department.
    /// </summary>
    HRManager = 3,

    /// <summary>
    /// Department-scoped manager. Dynamically created by Director/HRManager.
    /// Can approve leave/timesheets for their team. No payroll or compliance access.
    /// Also an employee of their managed department.
    /// </summary>
    Manager = 4,

    /// <summary>
    /// Base self-service tier. View own profile, payslips, leave requests.
    /// Cannot access timesheets, compliance, or audit trail.
    /// </summary>
    Employee = 5,
}
