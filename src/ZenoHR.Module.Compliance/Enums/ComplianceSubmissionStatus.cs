// REQ-COMP-001, CTL-SARS-006: Status lifecycle for a SARS compliance submission.

namespace ZenoHR.Module.Compliance.Enums;

/// <summary>
/// Lifecycle status of a <see cref="Entities.ComplianceSubmission"/>.
/// REQ-COMP-001: Submissions flow Pending → Submitted → Accepted | Rejected.
/// </summary>
public enum ComplianceSubmissionStatus
{
    Unknown = 0,

    /// <summary>Generated but not yet transmitted to SARS eFiling.</summary>
    Pending = 1,

    /// <summary>CSV/XML exported and reference number assigned — awaiting SARS confirmation.</summary>
    Submitted = 2,

    /// <summary>SARS has confirmed acceptance of the submission.</summary>
    Accepted = 3,

    /// <summary>SARS rejected the submission — see ComplianceFlags for rejection reasons.</summary>
    Rejected = 4,
}
