// CTL-POPIA-001: POPIA §11 lawful basis for processing personal information.
// Every data processing operation must reference one of these lawful bases.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// POPIA §11 lawful basis categories. A processing purpose must declare
/// exactly one lawful basis before any personal data may be processed.
/// </summary>
public enum LawfulBasis
{
    /// <summary>Default / unset — must never be accepted as valid.</summary>
    Unknown = 0,

    /// <summary>POPIA §11(1)(a) — data subject has given consent.</summary>
    Consent = 1,

    /// <summary>POPIA §11(1)(b) — processing necessary for contract performance.</summary>
    Contract = 2,

    /// <summary>POPIA §11(1)(c) — processing required by law (e.g., SARS, BCEA).</summary>
    LegalObligation = 3,

    /// <summary>POPIA §11(1)(d) — legitimate interest of the responsible party.</summary>
    LegitimateInterest = 4,

    /// <summary>POPIA §11(1)(e) — protecting vital interests of the data subject.</summary>
    VitalInterest = 5,

    /// <summary>POPIA §11(1)(f) — exercising a public law duty.</summary>
    PublicFunction = 6,
}
