// CTL-POPIA-010, CTL-POPIA-011: POPIA breach severity classification.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Severity levels for personal information security breaches under POPIA.
/// </summary>
public enum BreachSeverity
{
    Unknown = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
