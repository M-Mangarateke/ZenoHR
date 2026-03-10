// CTL-SARS-001, REQ-COMP-015
// TASK-138: Annual SARS tax year import + regression + activation workflow.

using ZenoHR.Domain.Errors;

namespace ZenoHR.Infrastructure.Services.TaxYear;

/// <summary>
/// Service that manages the annual SARS tax table import workflow:
/// validate → write pending → regression test → activate.
/// <para>
/// This is the authoritative entry point for any code that needs to update
/// SARS PAYE, UIF/SDL, or ETI rule sets in Firestore.
/// </para>
/// CTL-SARS-001: All statutory rates must flow through this service — never hardcoded.
/// REQ-COMP-015: Tax year import must be gated by regression testing before activation.
/// </summary>
public interface ITaxYearImportService
{
    /// <summary>
    /// Imports new SARS tax tables for the given tax year and, if regression passes,
    /// activates them in Firestore.
    /// <para>
    /// Steps:<br/>
    /// 1. Validate JSON structure (required keys, bracket count 3–10).<br/>
    /// 2. Write rule set to Firestore with status = "pending".<br/>
    /// 3. Run regression tests comparing old vs new PAYE for 5 representative gross amounts.<br/>
    /// 4. If regression passed → activate (status = "active").<br/>
    /// 5. Return <see cref="TaxYearImportResult"/> with full regression details.
    /// </para>
    /// </summary>
    /// <param name="request">Import request including JSON payloads and actor identity.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with <see cref="TaxYearImportResult"/> if import succeeded (even if regression failed).
    /// Failure if JSON is structurally invalid or Firestore write fails.
    /// </returns>
    Task<Result<TaxYearImportResult>> ImportAndActivateAsync(
        TaxYearImportRequest request, CancellationToken ct = default);

    /// <summary>
    /// Runs regression tests only — compares old vs new PAYE tables against 5 sample
    /// gross income amounts. Does NOT write to Firestore or activate any rule set.
    /// Safe to call any number of times as a preview before committing an import.
    /// </summary>
    /// <param name="currentTaxYear">
    /// The tax year label of the currently active rule set to compare against, e.g. "2026".
    /// </param>
    /// <param name="newPayeRuleSetJson">
    /// Raw JSON of the proposed new PAYE rule set (matching seed-data format).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<Result<TaxYearRegressionReport>> RunRegressionOnlyAsync(
        string currentTaxYear, string newPayeRuleSetJson, CancellationToken ct = default);

    /// <summary>
    /// Activates a previously imported (status = "pending") tax year rule set.
    /// <para>
    /// Fails if the rule set document does not exist or its status is not "pending".
    /// Regression must have been run and passed before calling this method.
    /// </para>
    /// </summary>
    /// <param name="taxYear">Tax year label, e.g. "2027".</param>
    /// <param name="activatedBy">firebase_uid of the actor activating the rule set.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<Result> ActivateAsync(string taxYear, string activatedBy, CancellationToken ct = default);
}
