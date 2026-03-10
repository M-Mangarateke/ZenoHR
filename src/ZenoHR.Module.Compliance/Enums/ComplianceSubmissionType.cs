// REQ-COMP-001, CTL-SARS-006: Type of SARS compliance submission.

namespace ZenoHR.Module.Compliance.Enums;

/// <summary>
/// Identifies the type of SARS compliance submission.
/// REQ-COMP-001: EMP201 (monthly) and EMP501 (annual reconciliation) are the two primary filing types.
/// </summary>
public enum ComplianceSubmissionType
{
    Unknown = 0,

    /// <summary>Monthly PAYE/UIF/SDL declaration — due by the 7th of the following month.</summary>
    Emp201 = 1,

    /// <summary>Annual reconciliation of all EMP201 filings against IRP5/IT3a certificates.</summary>
    Emp501 = 2,
}
