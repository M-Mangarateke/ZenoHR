// CTL-SARS-001, REQ-COMP-015
// TASK-138: Annual SARS tax year import + regression + activation workflow.

namespace ZenoHR.Infrastructure.Services.TaxYear;

/// <summary>
/// Result of a tax year import operation.
/// Reports whether regression passed, any warnings or errors encountered,
/// and whether the new rule set was activated in Firestore.
/// CTL-SARS-001, REQ-COMP-015
/// </summary>
public sealed record TaxYearImportResult
{
    /// <summary>Tax year label, e.g. "2027".</summary>
    public required string TaxYear { get; init; }

    /// <summary>Firestore document ID of the imported rule set, e.g. "SARS_PAYE_2027".</summary>
    public required string DocumentId { get; init; }

    /// <summary>True if regression tests passed (PAYE delta within acceptable thresholds).</summary>
    public required bool RegressionPassed { get; init; }

    /// <summary>
    /// Non-fatal warnings from regression — employee samples with PAYE changes > R200.
    /// HR Manager should review before activating in edge cases.
    /// </summary>
    public required IReadOnlyList<string> RegressionWarnings { get; init; }

    /// <summary>
    /// Structural or validation errors that caused regression to fail.
    /// Must be resolved before the rule set can be activated.
    /// </summary>
    public required IReadOnlyList<string> RegressionErrors { get; init; }

    /// <summary>True if the rule set was activated (status = "active") in Firestore.</summary>
    public required bool IsActivated { get; init; }

    /// <summary>The date from which this rule set becomes effective (defaults to March 1 of the tax year).</summary>
    public required DateOnly EffectiveFrom { get; init; }

    /// <summary>UTC timestamp of the import operation.</summary>
    public DateTimeOffset ImportedAt { get; init; } = DateTimeOffset.UtcNow;
}
