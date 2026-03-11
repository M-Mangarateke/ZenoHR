// CTL-SARS-010: SARS eFiling submission types supported by ZenoHR.

namespace ZenoHR.Module.Compliance.Services.EFiling;

/// <summary>
/// Identifies the SARS return type being submitted via eFiling.
/// </summary>
public enum EFilingSubmissionType
{
    /// <summary>Forward-compatibility default.</summary>
    Unknown = 0,

    /// <summary>Monthly employer declaration (PAYE, UIF, SDL).</summary>
    EMP201 = 1,

    /// <summary>Bi-annual employer reconciliation.</summary>
    EMP501 = 2,

    /// <summary>Employee tax certificate (IRP5 or IT3(a)).</summary>
    IRP5IT3a = 3,

    /// <summary>Request for cancellation of an employee tax certificate.</summary>
    EMP601 = 4,

    /// <summary>Prior-year employer reconciliation.</summary>
    EMP701 = 5,
}
