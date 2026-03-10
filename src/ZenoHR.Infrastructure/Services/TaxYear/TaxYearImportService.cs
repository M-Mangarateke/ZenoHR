// CTL-SARS-001, REQ-COMP-015
// TASK-138: Annual SARS tax year import + regression + activation workflow.

using System.Text.Json;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Module.Payroll.Calculation;
// Note: Google.Cloud.Firestore used for Timestamp in WritePendingAsync

namespace ZenoHR.Infrastructure.Services.TaxYear;

/// <summary>
/// Implements the annual SARS tax year import, regression testing, and activation workflow.
/// <para>
/// Workflow: validate JSON → write pending to Firestore → run regression against
/// 5 representative gross income samples → if passed, activate.
/// </para>
/// CTL-SARS-001: All tax rates flow through Firestore — never hardcoded.
/// REQ-COMP-015: New tax tables may only be activated after regression tests pass.
/// </summary>
public sealed partial class TaxYearImportService : ITaxYearImportService
{
    // Representative annual gross income samples for regression (ZAR).
    // Chosen to span the bracket table: below threshold, low-mid, mid, high, top.
    // CTL-SARS-001: Amounts chosen to hit different bracket bands — not arbitrary constants.
    private static readonly decimal[] RegressionSampleGross =
    [
        60_000m,      // Below tax threshold — expect R0 PAYE both sides
        150_000m,     // First bracket (18%)
        360_000m,     // Second bracket (26%)
        600_000m,     // Fourth bracket (36%)
        1_200_000m,   // Sixth bracket (41%)
    ];

    // Regression thresholds (annual PAYE difference per sample, ZAR)
    private const decimal WarningThreshold = 200m;
    private const decimal ErrorThreshold = 1_000m;
    private const decimal FailThreshold = 2_000m;

    // Standard age used for regression samples (under-65, primary rebate only)
    private const int RegressionAge = 35;

    private readonly StatutoryRuleSetRepository _ruleSetRepo;
    private readonly ILogger<TaxYearImportService> _logger;

    public TaxYearImportService(
        StatutoryRuleSetRepository ruleSetRepo,
        ILogger<TaxYearImportService> logger)
    {
        ArgumentNullException.ThrowIfNull(ruleSetRepo);
        ArgumentNullException.ThrowIfNull(logger);
        _ruleSetRepo = ruleSetRepo;
        _logger = logger;
    }

    // ── ImportAndActivateAsync ────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<TaxYearImportResult>> ImportAndActivateAsync(
        TaxYearImportRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        LogImportStarted(_logger, request.TaxYear, request.ImportedBy);

        // Step 1: Validate PAYE JSON structure
        var validationResult = ValidatePayeJson(request.PayeRuleSetJson);
        if (validationResult.IsFailure)
            return Result<TaxYearImportResult>.Failure(validationResult.Error);

        // Step 2: Determine effective date (default: March 1 of the tax year)
        var effectiveFrom = request.EffectiveFrom ?? ResolveDefaultEffectiveFrom(request.TaxYear);

        // Step 3: Write pending document to Firestore
        var documentId = $"SARS_PAYE_{request.TaxYear}";
        var writeResult = await WritePendingAsync(documentId, request, effectiveFrom, ct);
        if (writeResult.IsFailure)
            return Result<TaxYearImportResult>.Failure(writeResult.Error);

        // Step 4: Run regression against the currently active rule set
        var regressionResult = await RunRegressionOnlyAsync(
            currentTaxYear: DeriveCurrentTaxYear(request.TaxYear),
            newPayeRuleSetJson: request.PayeRuleSetJson,
            ct: ct);

        TaxYearRegressionReport regression;
        var regressionWarnings = new List<string>();
        var regressionErrors = new List<string>();
        var regressionPassed = false;

        if (regressionResult.IsSuccess)
        {
            regression = regressionResult.Value;
            regressionWarnings.AddRange(regression.Warnings);
            regressionErrors.AddRange(regression.Errors);
            regressionPassed = regression.Passed;
        }
        else
        {
            // Regression could not be run (e.g. no active rule set to compare against).
            // Treat as a non-fatal warning — allow import but do not auto-activate.
            regressionWarnings.Add($"Regression could not run: {regressionResult.Error.Message}");
            regressionPassed = false;
        }

        // Step 5: If regression passed, activate
        var activated = false;
        if (regressionPassed)
        {
            var activateResult = await ActivateAsync(request.TaxYear, request.ImportedBy, ct);
            activated = activateResult.IsSuccess;
            if (!activateResult.IsSuccess)
                regressionErrors.Add($"Activation failed: {activateResult.Error.Message}");
        }

        LogImportCompleted(_logger, request.TaxYear, regressionPassed, activated);

        return Result<TaxYearImportResult>.Success(new TaxYearImportResult
        {
            TaxYear = request.TaxYear,
            DocumentId = documentId,
            RegressionPassed = regressionPassed,
            RegressionWarnings = regressionWarnings.AsReadOnly(),
            RegressionErrors = regressionErrors.AsReadOnly(),
            IsActivated = activated,
            EffectiveFrom = effectiveFrom,
        });
    }

    // ── RunRegressionOnlyAsync ────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<TaxYearRegressionReport>> RunRegressionOnlyAsync(
        string currentTaxYear, string newPayeRuleSetJson, CancellationToken ct = default)
    {
        // Load the currently active PAYE rule set from Firestore
        var oldDocId = $"SARS_PAYE_{currentTaxYear}";
        var oldRuleSetResult = await _ruleSetRepo.GetByIdAsync(oldDocId, ct);
        if (oldRuleSetResult.IsFailure)
            return Result<TaxYearRegressionReport>.Failure(oldRuleSetResult.Error);

        var oldSarsRuleSet = SarsPayeRuleSet.From(oldRuleSetResult.Value);

        // Parse the new JSON into a typed rule set (without Firestore round-trip)
        var newRuleSetResult = ParsePayeJson(newPayeRuleSetJson);
        if (newRuleSetResult.IsFailure)
            return Result<TaxYearRegressionReport>.Failure(newRuleSetResult.Error);

        var newSarsRuleSet = newRuleSetResult.Value;

        // Compare PAYE for each sample gross income amount
        var samples = new List<RegressionSample>(RegressionSampleGross.Length);
        var warnings = new List<string>();
        var errors = new List<string>();
        var passed = true;

        foreach (var annualGross in RegressionSampleGross)
        {
            var monthlyGross = new MoneyZAR(annualGross / 12m);
            var oldMonthlyPaye = PayeCalculationEngine.CalculateMonthlyPAYE(
                monthlyGross, RegressionAge, oldSarsRuleSet);
            var newMonthlyPaye = PayeCalculationEngine.CalculateMonthlyPAYE(
                monthlyGross, RegressionAge, newSarsRuleSet);

            var oldAnnualPaye = oldMonthlyPaye.Amount * 12m;
            var newAnnualPaye = newMonthlyPaye.Amount * 12m;
            var diff = Math.Abs(newAnnualPaye - oldAnnualPaye);

            var sampleId = $"Sample_R{annualGross:F0}";
            samples.Add(new RegressionSample
            {
                EmployeeId = sampleId,
                AnnualGross = annualGross,
                OldAnnualPaye = oldAnnualPaye,
                NewAnnualPaye = newAnnualPaye,
            });

            if (diff > FailThreshold)
            {
                passed = false;
                errors.Add(
                    $"{sampleId}: annual PAYE changed by R{diff:F2} " +
                    $"(old R{oldAnnualPaye:F2}, new R{newAnnualPaye:F2}) — exceeds fail threshold R{FailThreshold:F2}.");
            }
            else if (diff > ErrorThreshold)
            {
                errors.Add(
                    $"{sampleId}: annual PAYE changed by R{diff:F2} " +
                    $"(old R{oldAnnualPaye:F2}, new R{newAnnualPaye:F2}) — exceeds error threshold R{ErrorThreshold:F2}.");
            }
            else if (diff > WarningThreshold)
            {
                warnings.Add(
                    $"{sampleId}: annual PAYE changed by R{diff:F2} " +
                    $"(old R{oldAnnualPaye:F2}, new R{newAnnualPaye:F2}) — review recommended.");
            }
        }

        var report = new TaxYearRegressionReport
        {
            OldTaxYear = currentTaxYear,
            NewTaxYear = newSarsRuleSet.TaxYear,
            EmployeeSamplesCompared = samples.Count,
            Passed = passed,
            Samples = samples.AsReadOnly(),
            Warnings = warnings.AsReadOnly(),
            Errors = errors.AsReadOnly(),
        };

        LogRegressionCompleted(_logger, currentTaxYear, newSarsRuleSet.TaxYear, passed, samples.Count);

        return Result<TaxYearRegressionReport>.Success(report);
    }

    // ── ActivateAsync ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result> ActivateAsync(
        string taxYear, string activatedBy, CancellationToken ct = default)
    {
        var documentId = $"SARS_PAYE_{taxYear}";

        // Delegate to repository — verifies existence and updates status atomically
        var result = await _ruleSetRepo.SetStatusAsync(documentId, "active", activatedBy, ct);
        if (result.IsFailure)
            return result;

        LogActivated(_logger, taxYear, documentId, activatedBy);
        return Result.Success();
    }

    // ── Pure static helpers (internal for unit-test access) ───────────────────

    /// <summary>
    /// Validates the structural requirements of a PAYE rule set JSON payload.
    /// Checks for required keys (tax_year, tax_brackets, rebates) and bracket count (3–10).
    /// CTL-SARS-001: JSON must conform to seed-data format.
    /// </summary>
    public static Result ValidatePayeJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tax_year", out _))
                return Result.Failure(ZenoHrErrorCode.ValidationFailed,
                    "PAYE JSON is missing required key 'tax_year'.");

            if (!root.TryGetProperty("tax_brackets", out var brackets))
                return Result.Failure(ZenoHrErrorCode.ValidationFailed,
                    "PAYE JSON is missing required key 'tax_brackets'.");

            if (brackets.ValueKind != JsonValueKind.Array)
                return Result.Failure(ZenoHrErrorCode.ValidationFailed,
                    "'tax_brackets' must be a JSON array.");

            var count = brackets.GetArrayLength();
            if (count < 3)
                return Result.Failure(ZenoHrErrorCode.ValidationFailed,
                    $"'tax_brackets' has {count} bracket(s) — minimum 3 required.");

            if (count > 10)
                return Result.Failure(ZenoHrErrorCode.ValidationFailed,
                    $"'tax_brackets' has {count} brackets — maximum 10 allowed.");

            if (!root.TryGetProperty("rebates", out _))
                return Result.Failure(ZenoHrErrorCode.ValidationFailed,
                    "PAYE JSON is missing required key 'rebates'.");

            return Result.Success();
        }
        catch (JsonException ex)
        {
            return Result.Failure(ZenoHrErrorCode.ValidationFailed,
                $"PAYE JSON is not valid JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses a PAYE rule set JSON string into a <see cref="SarsPayeRuleSet"/> without
    /// a Firestore round-trip. Used during regression so the new rule set can be tested
    /// immediately after upload without waiting for Firestore replication.
    /// CTL-SARS-001: All values extracted from provided JSON — no hardcoded defaults.
    /// </summary>
    public static Result<SarsPayeRuleSet> ParsePayeJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var taxYear = root.TryGetProperty("tax_year", out var ty)
                ? ty.GetString() ?? ""
                : "";

            // Parse tax_brackets
            if (!root.TryGetProperty("tax_brackets", out var bracketsEl))
                return Result<SarsPayeRuleSet>.Failure(ZenoHrErrorCode.ValidationFailed,
                    "Missing 'tax_brackets' in PAYE JSON.");

            var brackets = new List<PayeTaxBracket>();
            foreach (var b in bracketsEl.EnumerateArray())
            {
                var min = b.GetProperty("min").GetDecimal();
                decimal? max = b.TryGetProperty("max", out var maxEl) && maxEl.ValueKind != JsonValueKind.Null
                    ? maxEl.GetDecimal()
                    : (decimal?)null;
                var rate = b.GetProperty("rate").GetDecimal();
                var baseTax = b.GetProperty("base_tax").GetDecimal();
                brackets.Add(new PayeTaxBracket { Min = min, Max = max, Rate = rate, BaseTax = baseTax });
            }

            // Parse rebates
            if (!root.TryGetProperty("rebates", out var rebatesEl))
                return Result<SarsPayeRuleSet>.Failure(ZenoHrErrorCode.ValidationFailed,
                    "Missing 'rebates' in PAYE JSON.");

            var primary = rebatesEl.GetProperty("primary").GetDecimal();
            var secondary = rebatesEl.GetProperty("secondary_age_65_plus").GetDecimal();
            var tertiary = rebatesEl.GetProperty("tertiary_age_75_plus").GetDecimal();

            // Parse thresholds
            if (!root.TryGetProperty("tax_thresholds", out var thresholdsEl))
                return Result<SarsPayeRuleSet>.Failure(ZenoHrErrorCode.ValidationFailed,
                    "Missing 'tax_thresholds' in PAYE JSON.");

            var below65 = thresholdsEl.GetProperty("below_age_65").GetDecimal();
            var age65To74 = thresholdsEl.GetProperty("age_65_to_74").GetDecimal();
            var age75Plus = thresholdsEl.GetProperty("age_75_and_over").GetDecimal();

            var ruleSet = SarsPayeRuleSet.CreateForTesting(
                brackets: brackets.AsReadOnly(),
                primary: primary,
                secondary: secondary,
                tertiary: tertiary,
                thresholdBelow65: below65,
                thresholdAge65To74: age65To74,
                thresholdAge75Plus: age75Plus,
                taxYear: taxYear);

            return Result<SarsPayeRuleSet>.Success(ruleSet);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<SarsPayeRuleSet>.Failure(ZenoHrErrorCode.ValidationFailed,
                $"Failed to parse PAYE JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds regression samples by comparing PAYE under two typed rule sets
    /// for the standard set of representative annual gross income amounts.
    /// This pure static method is the testable core of the regression logic.
    /// Exposed as public to support unit testing without Firestore.
    /// CTL-SARS-001, REQ-COMP-015
    /// </summary>
    public static (IReadOnlyList<RegressionSample> Samples, bool Passed,
                   IReadOnlyList<string> Warnings, IReadOnlyList<string> Errors)
        BuildRegressionSamples(SarsPayeRuleSet oldRuleSet, SarsPayeRuleSet newRuleSet)
    {
        var samples = new List<RegressionSample>(RegressionSampleGross.Length);
        var warnings = new List<string>();
        var errors = new List<string>();
        var passed = true;

        foreach (var annualGross in RegressionSampleGross)
        {
            var monthlyGross = new MoneyZAR(annualGross / 12m);
            var oldMonthlyPaye = PayeCalculationEngine.CalculateMonthlyPAYE(
                monthlyGross, RegressionAge, oldRuleSet);
            var newMonthlyPaye = PayeCalculationEngine.CalculateMonthlyPAYE(
                monthlyGross, RegressionAge, newRuleSet);

            var oldAnnualPaye = oldMonthlyPaye.Amount * 12m;
            var newAnnualPaye = newMonthlyPaye.Amount * 12m;
            var diff = Math.Abs(newAnnualPaye - oldAnnualPaye);
            var sampleId = $"Sample_R{annualGross:F0}";

            samples.Add(new RegressionSample
            {
                EmployeeId = sampleId,
                AnnualGross = annualGross,
                OldAnnualPaye = oldAnnualPaye,
                NewAnnualPaye = newAnnualPaye,
            });

            if (diff > FailThreshold)
            {
                passed = false;
                errors.Add(
                    $"{sampleId}: annual PAYE changed by R{diff:F2} " +
                    $"(old R{oldAnnualPaye:F2}, new R{newAnnualPaye:F2}) — exceeds fail threshold R{FailThreshold:F2}.");
            }
            else if (diff > ErrorThreshold)
            {
                errors.Add(
                    $"{sampleId}: annual PAYE changed by R{diff:F2} " +
                    $"(old R{oldAnnualPaye:F2}, new R{newAnnualPaye:F2}) — exceeds error threshold R{ErrorThreshold:F2}.");
            }
            else if (diff > WarningThreshold)
            {
                warnings.Add(
                    $"{sampleId}: annual PAYE changed by R{diff:F2} " +
                    $"(old R{oldAnnualPaye:F2}, new R{newAnnualPaye:F2}) — review recommended.");
            }
        }

        return (samples.AsReadOnly(), passed, warnings.AsReadOnly(), errors.AsReadOnly());
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Writes a pending PAYE rule set document to Firestore via the repository.
    /// The document is written with status = "pending" — activation is a separate step.
    /// CTL-SARS-001
    /// </summary>
    private async Task<Result> WritePendingAsync(
        string documentId,
        TaxYearImportRequest request,
        DateOnly effectiveFrom,
        CancellationToken ct)
    {
        var now = Timestamp.GetCurrentTimestamp();
        var fields = new Dictionary<string, object?>
        {
            ["tenant_id"] = "SYSTEM",
            ["rule_domain"] = RuleDomains.SarsPaye,
            ["tax_year"] = request.TaxYear,
            ["version"] = $"{request.TaxYear}.1.0",
            ["effective_from"] = effectiveFrom.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ["status"] = "pending",
            ["imported_by"] = request.ImportedBy,
            ["imported_at"] = now,
            ["created_at"] = now,
            ["paye_rule_set_json"] = request.PayeRuleSetJson,
            ["uif_sdl_rule_set_json"] = request.UifSdlRuleSetJson,
            ["eti_rule_set_json"] = request.EtiRuleSetJson,
        };

        var result = await _ruleSetRepo.UpsertPendingTaxYearAsync(documentId, fields, ct);
        if (result.IsSuccess)
            LogPendingWritten(_logger, documentId, request.TaxYear);
        return result;
    }

    /// <summary>
    /// Resolves the default effective date (March 1) from the tax year label.
    /// Tax year "2027" → effective from 2026-03-01.
    /// CTL-SARS-001
    /// </summary>
    private static DateOnly ResolveDefaultEffectiveFrom(string taxYear)
    {
        if (int.TryParse(taxYear, out var year))
            return new DateOnly(year - 1, 3, 1);  // March 1 of the year before the tax year label

        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

    /// <summary>
    /// Derives the currently active tax year from the incoming tax year (subtract 1).
    /// Tax year "2027" → compare against "2026".
    /// </summary>
    private static string DeriveCurrentTaxYear(string taxYear) =>
        int.TryParse(taxYear, out var year)
            ? (year - 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : taxYear;

    // ── Structured logging (source-generated) ─────────────────────────────────

    [LoggerMessage(EventId = 5100, Level = LogLevel.Information,
        Message = "TaxYearImport started: tax_year={TaxYear}, imported_by={ImportedBy}")]
    private static partial void LogImportStarted(ILogger logger, string taxYear, string importedBy);

    [LoggerMessage(EventId = 5101, Level = LogLevel.Information,
        Message = "TaxYearImport pending document written: {DocumentId} for tax_year={TaxYear}")]
    private static partial void LogPendingWritten(ILogger logger, string documentId, string taxYear);

    [LoggerMessage(EventId = 5102, Level = LogLevel.Information,
        Message = "TaxYearRegression completed: old={OldTaxYear} → new={NewTaxYear}, passed={Passed}, samples={SampleCount}")]
    private static partial void LogRegressionCompleted(
        ILogger logger, string oldTaxYear, string newTaxYear, bool passed, int sampleCount);

    [LoggerMessage(EventId = 5103, Level = LogLevel.Information,
        Message = "TaxYearImport completed: tax_year={TaxYear}, regression_passed={RegressionPassed}, activated={Activated}")]
    private static partial void LogImportCompleted(
        ILogger logger, string taxYear, bool regressionPassed, bool activated);

    [LoggerMessage(EventId = 5104, Level = LogLevel.Information,
        Message = "TaxYearActivated: document_id={DocumentId}, tax_year={TaxYear}, activated_by={ActivatedBy}")]
    private static partial void LogActivated(
        ILogger logger, string taxYear, string documentId, string activatedBy);
}
