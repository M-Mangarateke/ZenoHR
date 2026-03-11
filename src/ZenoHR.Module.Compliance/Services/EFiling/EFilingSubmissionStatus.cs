// CTL-SARS-010: Tracks lifecycle status of a SARS eFiling submission.

namespace ZenoHR.Module.Compliance.Services.EFiling;

/// <summary>
/// Lifecycle status of a SARS eFiling submission.
/// Transitions: Pending -> Submitted -> Accepted/Rejected/Error.
/// </summary>
public enum EFilingSubmissionStatus
{
    /// <summary>Forward-compatibility default.</summary>
    Unknown = 0,

    /// <summary>Submission created but not yet sent to SARS.</summary>
    Pending = 1,

    /// <summary>Submission sent to SARS, awaiting acknowledgement.</summary>
    Submitted = 2,

    /// <summary>SARS accepted the submission.</summary>
    Accepted = 3,

    /// <summary>SARS rejected the submission (see error message for details).</summary>
    Rejected = 4,

    /// <summary>A transport or system error occurred during submission.</summary>
    Error = 5,
}
