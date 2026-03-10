// CTL-SARS-001, REQ-COMP-015
// TASK-138: Annual SARS tax year import + regression + activation workflow.

namespace ZenoHR.Infrastructure.Services.TaxYear;

/// <summary>
/// Request to import a new SARS PAYE tax year rule set.
/// The HR Manager or Director submits this when SARS publishes updated rates
/// (typically after the February budget speech, effective March 1).
/// CTL-SARS-001: Tax tables must come from Firestore — never hardcoded.
/// REQ-COMP-015: Tax year import must be gated by regression testing.
/// </summary>
public sealed record TaxYearImportRequest
{
    /// <summary>The tax year string, e.g. "2027" (March 2026 – Feb 2027).</summary>
    public required string TaxYear { get; init; }

    /// <summary>Raw JSON of the new PAYE rule set (matching seed-data format).</summary>
    public required string PayeRuleSetJson { get; init; }

    /// <summary>Raw JSON of the new UIF/SDL rule set.</summary>
    public required string UifSdlRuleSetJson { get; init; }

    /// <summary>Raw JSON of the new ETI rule set.</summary>
    public required string EtiRuleSetJson { get; init; }

    /// <summary>Actor (firebase_uid) importing the tables.</summary>
    public required string ImportedBy { get; init; }

    /// <summary>
    /// Optional: override effective date. Defaults to March 1 of the tax year.
    /// For tax year "2027" the default is 2026-03-01.
    /// </summary>
    public DateOnly? EffectiveFrom { get; init; }
}
