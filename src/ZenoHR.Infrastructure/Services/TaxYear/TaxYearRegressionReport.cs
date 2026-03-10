// CTL-SARS-001, REQ-COMP-015
// TASK-138: Annual SARS tax year import + regression + activation workflow.

namespace ZenoHR.Infrastructure.Services.TaxYear;

/// <summary>
/// Report from comparing old vs new SARS PAYE tax tables against a set of
/// representative annual gross income samples.
/// <para>
/// Regression thresholds (per sample):<br/>
/// Warning  : annual PAYE difference &gt; R200<br/>
/// Error    : annual PAYE difference &gt; R1,000<br/>
/// Fail     : annual PAYE difference &gt; R2,000 for any sample
/// </para>
/// CTL-SARS-001, REQ-COMP-015
/// </summary>
public sealed record TaxYearRegressionReport
{
    /// <summary>Tax year label for the currently active (outgoing) rule set, e.g. "2026".</summary>
    public required string OldTaxYear { get; init; }

    /// <summary>Tax year label for the new (incoming) rule set, e.g. "2027".</summary>
    public required string NewTaxYear { get; init; }

    /// <summary>Number of representative income samples compared.</summary>
    public required int EmployeeSamplesCompared { get; init; }

    /// <summary>
    /// True if no sample's annual PAYE changed by more than R2,000.
    /// HR Manager must not activate a rule set where Passed == false.
    /// </summary>
    public required bool Passed { get; init; }

    /// <summary>Per-sample comparison results.</summary>
    public required IReadOnlyList<RegressionSample> Samples { get; init; }

    /// <summary>
    /// Non-fatal warnings — samples where annual PAYE changed by more than R200
    /// but not more than R2,000. Should be reviewed but do not block activation.
    /// </summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>
    /// Fatal errors — structural JSON errors or samples where PAYE changed by more
    /// than R2,000. Block activation until resolved.
    /// </summary>
    public required IReadOnlyList<string> Errors { get; init; }

    /// <summary>UTC timestamp when the regression report was generated.</summary>
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A single sample record comparing annual PAYE under the old and new rule sets
/// for a representative annual gross income.
/// CTL-SARS-001, REQ-COMP-015
/// </summary>
public sealed record RegressionSample
{
    /// <summary>
    /// A human-readable identifier for this sample, e.g. "Sample_R60000" or an employee ID.
    /// </summary>
    public required string EmployeeId { get; init; }

    /// <summary>Annual gross income used for this comparison (ZAR). Always decimal — never float.</summary>
    public required decimal AnnualGross { get; init; }

    /// <summary>Annual PAYE calculated under the old (outgoing) rule set (ZAR).</summary>
    public required decimal OldAnnualPaye { get; init; }

    /// <summary>Annual PAYE calculated under the new (incoming) rule set (ZAR).</summary>
    public required decimal NewAnnualPaye { get; init; }

    /// <summary>Signed difference: new minus old (ZAR). Positive = employee pays more tax.</summary>
    public decimal PayeDifference => NewAnnualPaye - OldAnnualPaye;

    /// <summary>True if the absolute difference exceeds R500 — used for UI highlighting.</summary>
    public bool IsMaterialChange => Math.Abs(PayeDifference) > 500m;
}
