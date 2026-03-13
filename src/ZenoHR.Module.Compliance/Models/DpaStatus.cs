// CTL-POPIA-013, CTL-POPIA-014: Data Processing Agreement status for processor inventory.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Status of a Data Processing Agreement (DPA) with a third-party processor.
/// POPIA Section 21 requires written agreements with all operators processing personal information.
/// </summary>
public enum DpaStatus
{
    Unknown = 0,
    NotRequired = 1,
    Required = 2,
    Obtained = 3,
    Expired = 4
}
