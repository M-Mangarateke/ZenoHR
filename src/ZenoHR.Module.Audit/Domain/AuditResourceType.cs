// REQ-OPS-003: AuditResourceType — categorises the Firestore collection / domain entity being audited.
// Used with AuditAction to form the audit context: "who did what to which type of resource".

namespace ZenoHR.Module.Audit.Domain;

/// <summary>
/// Identifies the type of resource targeted by an <see cref="AuditEvent"/>.
/// Maps 1-to-1 with Firestore root collections and significant sub-collections.
/// </summary>
public enum AuditResourceType
{
    /// <summary>Unknown resource type — forward-compatibility sentinel.</summary>
    Unknown = 0,

    /// <summary>Employee root document (<c>employees/{emp_id}</c>).</summary>
    Employee = 1,

    /// <summary>Employment contract sub-document (<c>employees/{emp_id}/contracts/{contract_id}</c>).</summary>
    EmploymentContract = 2,

    /// <summary>Bank account sub-document (<c>employees/{emp_id}/bank_accounts/{account_id}</c>).</summary>
    BankAccount = 3,

    /// <summary>Leave request sub-document (<c>employees/{emp_id}/leave_requests/{req_id}</c>).</summary>
    LeaveRequest = 4,

    /// <summary>Leave balance document (<c>leave_balances/{balance_id}</c>).</summary>
    LeaveBalance = 5,

    /// <summary>Payroll run root document (<c>payroll_runs/{run_id}</c>).</summary>
    PayrollRun = 6,

    /// <summary>Payroll result sub-document (<c>payroll_runs/{run_id}/results/{result_id}</c>).</summary>
    PayrollResult = 7,

    /// <summary>Compliance submission document (<c>compliance_submissions/{submission_id}</c>).</summary>
    ComplianceSubmission = 8,

    /// <summary>Department configuration document.</summary>
    Department = 9,

    /// <summary>User role assignment document (<c>user_role_assignments/{assignment_id}</c>).</summary>
    UserRoleAssignment = 10,

    /// <summary>Clock entry root document (<c>clock_entries/{entry_id}</c>).</summary>
    ClockEntry = 11,

    /// <summary>Timesheet flag root document (<c>timesheet_flags/{flag_id}</c>).</summary>
    TimesheetFlag = 12,

    /// <summary>Statutory rule set document (<c>statutory_rule_sets/{rule_id}</c>).</summary>
    StatutoryRuleSet = 13,

    /// <summary>System configuration or company settings document.</summary>
    SystemConfiguration = 14,

    /// <summary>Benefits sub-document (<c>employees/{emp_id}/benefits/{benefit_id}</c>).</summary>
    EmployeeBenefit = 15,

    /// <summary>Next of kin sub-document (<c>employees/{emp_id}/next_of_kin/{nok_id}</c>).</summary>
    NextOfKin = 16,
}
