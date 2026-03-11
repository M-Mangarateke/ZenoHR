// CTL-SARS-004: Tax directive lifecycle status — forward-only state machine.
namespace ZenoHR.Module.Payroll.Models;

/// <summary>
/// Lifecycle status of a <see cref="TaxDirective"/>.
/// State machine: <c>Pending → Active → Expired → Revoked</c>.
/// Only forward transitions are permitted.
/// </summary>
public enum TaxDirectiveStatus
{
    /// <summary>Guard value — should never appear on a persisted document.</summary>
    Unknown = 0,

    /// <summary>Directive registered but not yet effective.</summary>
    Pending = 1,

    /// <summary>Directive is currently in effect and affects PAYE calculation.</summary>
    Active = 2,

    /// <summary>Directive has passed its EffectiveTo date.</summary>
    Expired = 3,

    /// <summary>Directive was revoked by SARS or employer before expiry.</summary>
    Revoked = 4
}
