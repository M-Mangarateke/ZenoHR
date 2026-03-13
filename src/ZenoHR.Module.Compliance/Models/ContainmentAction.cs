// CTL-POPIA-012, REQ-SEC-009: Containment actions taken during incident response.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Types of containment actions that can be executed during incident response.
/// </summary>
public enum ContainmentActionType
{
    Unknown = 0,
    RevokeUserToken = 1,
    FreezePayrollRun = 2,
    DisableAccount = 3,
    RestrictAccess = 4
}

/// <summary>
/// Immutable record of a containment action performed during incident response.
/// </summary>
public sealed record ContainmentAction
{
    public required string ActionId { get; init; }
    public required ContainmentActionType ActionType { get; init; }
    public required string TargetId { get; init; }
    public required string PerformedBy { get; init; }
    public required DateTimeOffset PerformedAt { get; init; }
    public string? Notes { get; init; }
}
