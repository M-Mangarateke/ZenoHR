// CTL-SARS-004: SARS IRP3 tax directive types — 4 categories of special withholding instructions.
namespace ZenoHR.Module.Payroll.Models;

/// <summary>
/// SARS IRP3 tax directive types issued to employers for special PAYE withholding.
/// </summary>
public enum TaxDirectiveType
{
    /// <summary>Guard value — should never appear on a persisted document.</summary>
    Unknown = 0,

    /// <summary>IRP3(a) — Lump sum from retirement fund (gratuity, severance).</summary>
    IRP3a = 1,

    /// <summary>IRP3(b) — Lump sum from pension/provident fund on withdrawal.</summary>
    IRP3b = 2,

    /// <summary>IRP3(c) — Commission earners (variable income PAYE basis).</summary>
    IRP3c = 3,

    /// <summary>IRP3(s) — Special salary payment arrangements (fixed percentage).</summary>
    IRP3s = 4
}
