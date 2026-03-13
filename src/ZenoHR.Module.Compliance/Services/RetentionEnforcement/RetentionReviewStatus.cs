// CTL-POPIA-015: Retention review workflow status.
// Tracks the lifecycle of a retention review from identification through disposition.

namespace ZenoHR.Module.Compliance.Services.RetentionEnforcement;

/// <summary>
/// Status of a retention review record, representing the workflow from
/// identification of expired data through to final disposition.
/// </summary>
public enum RetentionReviewStatus
{
    /// <summary>Forward-compatible default.</summary>
    Unknown = 0,

    /// <summary>Record identified as past retention — awaiting HR Manager review.</summary>
    Pending = 1,

    /// <summary>HR Manager approved anonymisation — ready for processing.</summary>
    Approved = 2,

    /// <summary>Personal data has been anonymised (irreversible).</summary>
    Anonymised = 3,

    /// <summary>Retention extended — record retained beyond standard period with documented reason.</summary>
    Retained = 4
}
