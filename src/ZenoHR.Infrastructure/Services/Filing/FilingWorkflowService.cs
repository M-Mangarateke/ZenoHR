// REQ-COMP-001, REQ-COMP-002, CTL-SARS-006: Filing workflow orchestration.
// TASK-091: Orchestrates EMP201 and EMP501 generation workflows end-to-end.
// Reads finalized PayrollRun + PayrollResult data, calls generators, persists ComplianceSubmission.
// CultureInfo.InvariantCulture used for all string formatting (CA1305 compliance).

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Infrastructure.Services.Filing.Emp201;
using ZenoHR.Infrastructure.Services.Filing.Emp501;
using ZenoHR.Module.Compliance.Entities;
using ZenoHR.Module.Compliance.Enums;
using ZenoHR.Module.Payroll.Aggregates;
using ZenoHR.Module.Payroll.Entities;

namespace ZenoHR.Infrastructure.Services.Filing;

/// <summary>
/// Orchestrates the end-to-end EMP201 and EMP501 SARS filing workflows.
/// REQ-COMP-001: Reads finalized payroll data → calls generators → persists ComplianceSubmission.
/// REQ-COMP-002: EMP501 reads all 12 periods of the tax year and produces annual reconciliation.
/// CTL-SARS-006: All generated files are SHA-256 checksummed before persistence.
/// </summary>
public sealed partial class FilingWorkflowService
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly ComplianceSubmissionRepository _submissionRepo;
    private readonly IEmp201Generator _emp201Generator;
    private readonly IEmp501Generator _emp501Generator;
    private readonly FirestoreDb _db;
    private readonly PayrollRunRepository _runRepo;
    private readonly PayrollResultRepository _resultRepo;
    private readonly ILogger<FilingWorkflowService> _logger;

    public FilingWorkflowService(
        ComplianceSubmissionRepository submissionRepo,
        IEmp201Generator emp201Generator,
        IEmp501Generator emp501Generator,
        FirestoreDb db,
        PayrollRunRepository runRepo,
        PayrollResultRepository resultRepo,
        ILogger<FilingWorkflowService> logger)
    {
        ArgumentNullException.ThrowIfNull(submissionRepo);
        ArgumentNullException.ThrowIfNull(emp201Generator);
        ArgumentNullException.ThrowIfNull(emp501Generator);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(runRepo);
        ArgumentNullException.ThrowIfNull(resultRepo);
        ArgumentNullException.ThrowIfNull(logger);

        _submissionRepo = submissionRepo;
        _emp201Generator = emp201Generator;
        _emp501Generator = emp501Generator;
        _db = db;
        _runRepo = runRepo;
        _resultRepo = resultRepo;
        _logger = logger;
    }

    // ── EMP201 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates an EMP201 monthly PAYE/UIF/SDL declaration for the given period.
    /// REQ-COMP-001: Reads finalized PayrollRun + per-employee PayrollResult subcollection.
    /// Steps:
    ///   1. Load finalized PayrollRun for the period.
    ///   2. Load all PayrollResult sub-documents.
    ///   3. Build Emp201Data from payroll totals.
    ///   4. Generate CSV via Emp201Generator.
    ///   5. SHA-256 checksum the CSV.
    ///   6. Persist ComplianceSubmission.
    /// </summary>
    public async Task<Result<ComplianceSubmission>> GenerateEmp201Async(
        string tenantId, string period, string actorId, CancellationToken ct = default)
    {
        // Step 1: Load finalized PayrollRun for the period
        var runs = await _runRepo.ListByPeriodAsync(tenantId, period, ct);
        var run = runs.FirstOrDefault(r =>
            r.Status == PayrollRunStatus.Finalized || r.Status == PayrollRunStatus.Filed);

        if (run is null)
        {
            LogNoFinalizedRunWarning(_logger, tenantId, period);
            return Result<ComplianceSubmission>.Failure(
                ZenoHrErrorCode.PayrollRunNotFound,
                string.Format(Invariant,
                    "No finalized payroll run found for tenant {0} period {1}.", tenantId, period));
        }

        // Step 2: Load all per-employee PayrollResult sub-documents
        var results = await _resultRepo.ListByRunAsync(run.Id, ct);

        // Step 3: Build Emp201Data from payroll totals
        // CTL-SARS-006: period format "YYYY-MM" → TaxPeriod "YYYYMM"
        var taxPeriod = period.Replace("-", "", StringComparison.Ordinal);
        var dueDate = ParseDueDateFromPeriod(period);

        // Build per-employee lines from PayrollResult list
        var employeeLines = results.Select(r => new Emp201EmployeeLine
        {
            EmployeeId = r.EmployeeId,
            EmployeeFullName = r.EmployeeId,      // Name lookup not in scope for this task
            TaxReferenceNumber = "—",              // From employee profile — stub for now
            IdOrPassportNumber = "—",              // From employee profile — stub for now
            GrossRemuneration = r.GrossPay.Amount,
            PayeDeducted = r.Paye.Amount,
            UifEmployee = r.UifEmployee.Amount,
            UifEmployer = r.UifEmployer.Amount,
            SdlEmployer = r.Sdl.Amount,
            PaymentMethod = "EFT",
        }).ToList();

        var emp201Data = new Emp201Data
        {
            EmployerPAYEReference = "—",           // From company settings — stub for now
            EmployerUifReference = "—",
            EmployerSdlReference = "—",
            EmployerTradingName = tenantId,
            TaxPeriod = taxPeriod,
            PeriodLabel = period,
            PayrollRunId = run.Id,
            TotalPayeDeducted = run.PayeTotal.Amount,
            TotalUifEmployee = results.Sum(r => r.UifEmployee.Amount),
            TotalUifEmployer = results.Sum(r => r.UifEmployer.Amount),
            TotalSdl = run.SdlTotal.Amount,
            TotalGrossRemuneration = run.GrossTotal.Amount,
            EmployeeCount = run.EmployeeCount,
            DueDate = dueDate,
            EmployeeLines = employeeLines.AsReadOnly(),
            GeneratedByUserId = actorId,
            GeneratedAt = DateTimeOffset.UtcNow,
        };

        // Step 4: Generate CSV
        string csv;
        try
        {
            csv = _emp201Generator.GenerateCsv(emp201Data);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ComplianceSubmission>.Failure(
                ZenoHrErrorCode.ComplianceCheckFailed,
                string.Format(Invariant, "EMP201 invariant violation: {0}", ex.Message));
        }

        // Step 5: SHA-256 checksum
        var csvBytes = Encoding.UTF8.GetBytes(csv);
        var checksum = ComputeSha256Hex(csvBytes);

        // Step 6: Create and persist ComplianceSubmission
        var submissionResult = ComplianceSubmission.Create(
            id: string.Format(Invariant, "cs_{0}_{1}_emp201", tenantId, period),
            tenantId: tenantId,
            period: period,
            submissionType: ComplianceSubmissionType.Emp201,
            payeAmount: run.PayeTotal,
            uifAmount: new MoneyZAR(results.Sum(r => r.UifEmployee.Amount + r.UifEmployer.Amount)),
            sdlAmount: run.SdlTotal,
            grossAmount: run.GrossTotal,
            employeeCount: run.EmployeeCount,
            checksumSha256: checksum,
            generatedFileContent: csvBytes,
            complianceFlags: null,
            createdBy: actorId,
            createdAt: DateTimeOffset.UtcNow);

        if (submissionResult.IsFailure)
            return submissionResult;

        return await _submissionRepo.CreateAsync(submissionResult.Value, ct);
    }

    // ── EMP501 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates an EMP501 annual reconciliation for the given SA tax year.
    /// REQ-COMP-002: Tax year "2026" covers March 2025 (2025-03) through February 2026 (2026-02).
    /// Steps:
    ///   1. Determine the 12 monthly periods for the tax year.
    ///   2. Load all finalized PayrollRun documents for those periods.
    ///   3. Load all PayrollResult sub-documents for each run.
    ///   4. Build Emp501Data with monthly summary + per-employee annual totals.
    ///   5. Generate reconciliation CSV.
    ///   6. SHA-256 checksum the CSV.
    ///   7. Persist ComplianceSubmission.
    /// </summary>
    public async Task<Result<ComplianceSubmission>> GenerateEmp501Async(
        string tenantId, string taxYear, string actorId, CancellationToken ct = default)
    {
        // Step 1: Determine the 12 periods for the tax year
        // SA tax year: "2026" → 2025-03 through 2026-02
        if (!int.TryParse(taxYear, NumberStyles.Integer, Invariant, out var taxYearInt))
        {
            return Result<ComplianceSubmission>.Failure(
                ZenoHrErrorCode.InvalidFilingPeriod,
                string.Format(Invariant, "Invalid tax year format '{0}'. Must be a 4-digit year.", taxYear));
        }

        var periods = BuildTaxYearPeriods(taxYearInt);

        // Step 2: Load finalized PayrollRun documents for each period
        var runsByPeriod = new Dictionary<string, PayrollRun>(StringComparer.Ordinal);
        foreach (var period in periods)
        {
            var runs = await _runRepo.ListByPeriodAsync(tenantId, period, ct);
            var run = runs.FirstOrDefault(r =>
                r.Status == PayrollRunStatus.Finalized || r.Status == PayrollRunStatus.Filed);
            if (run is not null)
                runsByPeriod[period] = run;
        }

        // Step 3: Load all PayrollResult sub-documents for each run
        var resultsByRunId = new Dictionary<string, IReadOnlyList<PayrollResult>>(StringComparer.Ordinal);
        foreach (var (_, run) in runsByPeriod)
        {
            var results = await _resultRepo.ListByRunAsync(run.Id, ct);
            resultsByRunId[run.Id] = results;
        }

        // Step 4: Build Emp501Data — monthly summary + per-employee annual totals
        var monthlyEntries = periods.Select(period =>
        {
            if (runsByPeriod.TryGetValue(period, out var run))
            {
                var results = resultsByRunId.TryGetValue(run.Id, out var r) ? r : [];
                return new Emp201MonthlyEntry
                {
                    Period = period,
                    TotalGross = run.GrossTotal.Amount,
                    TotalPaye = run.PayeTotal.Amount,
                    TotalUifEmployee = results.Sum(r2 => r2.UifEmployee.Amount),
                    TotalUifEmployer = results.Sum(r2 => r2.UifEmployer.Amount),
                    TotalSdl = run.SdlTotal.Amount,
                    Filed = run.Status == PayrollRunStatus.Filed,
                    FiledDate = run.FiledAt.HasValue
                        ? DateOnly.FromDateTime(run.FiledAt.Value.UtcDateTime)
                        : null,
                };
            }
            // Period exists in tax year but no payroll run found — unfiled month
            return new Emp201MonthlyEntry
            {
                Period = period,
                TotalGross = 0m,
                TotalPaye = 0m,
                TotalUifEmployee = 0m,
                TotalUifEmployer = 0m,
                TotalSdl = 0m,
                Filed = false,
                FiledDate = null,
            };
        }).ToList();

        // Per-employee annual aggregation — sum across all runs
        var employeeTotals = new Dictionary<string, Emp501EmployeeEntry>(StringComparer.Ordinal);
        int certSeq = 1;
        foreach (var (runId, results) in resultsByRunId)
        {
            foreach (var result in results)
            {
                if (employeeTotals.TryGetValue(result.EmployeeId, out var existing))
                {
                    // Accumulate into existing entry
                    employeeTotals[result.EmployeeId] = existing with
                    {
                        AnnualGross = existing.AnnualGross + result.GrossPay.Amount,
                        AnnualPaye = existing.AnnualPaye + result.Paye.Amount,
                        AnnualUifEmployee = existing.AnnualUifEmployee + result.UifEmployee.Amount,
                        AnnualUifEmployer = existing.AnnualUifEmployer + result.UifEmployer.Amount,
                        AnnualSdl = existing.AnnualSdl + result.Sdl.Amount,
                        AnnualEti = existing.AnnualEti + result.EtiAmount.Amount,
                        AnnualMedicalCredit = existing.AnnualMedicalCredit,
                    };
                }
                else
                {
                    employeeTotals[result.EmployeeId] = new Emp501EmployeeEntry
                    {
                        EmployeeId = result.EmployeeId,
                        EmployeeName = result.EmployeeId,   // Name lookup not in scope
                        IdNumber = "—",                      // From employee profile — stub
                        TaxRef = "—",
                        AnnualGross = result.GrossPay.Amount,
                        AnnualPaye = result.Paye.Amount,
                        AnnualUifEmployee = result.UifEmployee.Amount,
                        AnnualUifEmployer = result.UifEmployer.Amount,
                        AnnualSdl = result.Sdl.Amount,
                        AnnualEti = result.EtiAmount.Amount,
                        AnnualMedicalCredit = 0m,
                        Irp5Code = "IRP5",
                        // CTL-SARS-006: Certificate number unique per employee per tax year
                        CertificateNumber = string.Format(Invariant,
                            "{0}-{1}-{2:D4}", taxYear, result.EmployeeId, certSeq++),
                    };
                }
            }
        }

        var emp501Data = new Emp501Data
        {
            TenantId = tenantId,
            EmployerTaxRef = "—",               // From company settings — stub
            EmployerName = tenantId,
            EmployerAddress = "—",
            TaxYear = taxYear,
            MonthlySubmissions = monthlyEntries.AsReadOnly(),
            EmployeeEntries = employeeTotals.Values.ToList().AsReadOnly(),
        };

        // Step 5: Generate reconciliation CSV
        var csv = _emp501Generator.GenerateReconciliationCsv(emp501Data);

        // Step 6: SHA-256 checksum
        var csvBytes = Encoding.UTF8.GetBytes(csv);
        var checksum = ComputeSha256Hex(csvBytes);

        // Aggregate totals for submission
        var totalPaye = new MoneyZAR(employeeTotals.Values.Sum(e => e.AnnualPaye));
        var totalUif = new MoneyZAR(employeeTotals.Values.Sum(e => e.AnnualUifEmployee + e.AnnualUifEmployer));
        var totalSdl = new MoneyZAR(employeeTotals.Values.Sum(e => e.AnnualSdl));
        var totalGross = new MoneyZAR(employeeTotals.Values.Sum(e => e.AnnualGross));

        // Step 7: Create and persist ComplianceSubmission
        var submissionResult = ComplianceSubmission.Create(
            id: string.Format(Invariant, "cs_{0}_{1}_emp501", tenantId, taxYear),
            tenantId: tenantId,
            period: taxYear,
            submissionType: ComplianceSubmissionType.Emp501,
            payeAmount: totalPaye,
            uifAmount: totalUif,
            sdlAmount: totalSdl,
            grossAmount: totalGross,
            employeeCount: employeeTotals.Count,
            checksumSha256: checksum,
            generatedFileContent: csvBytes,
            complianceFlags: null,
            createdBy: actorId,
            createdAt: DateTimeOffset.UtcNow);

        if (submissionResult.IsFailure)
            return submissionResult;

        return await _submissionRepo.CreateAsync(submissionResult.Value, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the 12 monthly period strings for a SA tax year.
    /// Tax year "2026" = March 2025 (2025-03) through February 2026 (2026-02).
    /// REQ-COMP-002: All 12 months must be present in the EMP501 reconciliation.
    /// </summary>
    private static System.Collections.ObjectModel.ReadOnlyCollection<string> BuildTaxYearPeriods(int taxYear)
    {
        var periods = new List<string>(12);
        for (var month = 3; month <= 12; month++)
            periods.Add(string.Format(Invariant, "{0}-{1:D2}", taxYear - 1, month));
        for (var month = 1; month <= 2; month++)
            periods.Add(string.Format(Invariant, "{0}-{1:D2}", taxYear, month));
        return periods.AsReadOnly();
    }

    /// <summary>
    /// Calculates the SARS EMP201 due date from a "YYYY-MM" period string.
    /// CTL-SARS-006: 7th of the following month; advanced to Monday if Saturday/Sunday.
    /// </summary>
    private DateOnly ParseDueDateFromPeriod(string period)
    {
        // period format: "YYYY-MM"
        if (period.Length >= 7
            && int.TryParse(period[..4], NumberStyles.Integer, Invariant, out var year)
            && int.TryParse(period[5..7], NumberStyles.Integer, Invariant, out var month))
        {
            return _emp201Generator.CalculateDueDate(year, month);
        }
        // Fallback: return today + 7 days
        return DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
    }

    /// <summary>Computes the SHA-256 hex digest of the given bytes. CTL-SARS-006.</summary>
    private static string ComputeSha256Hex(byte[] data)
    {
        var hashBytes = SHA256.HashData(data);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    // REQ-COMP-001: High-performance logger messages (CA1848 compliance).
    [LoggerMessage(EventId = 9000, Level = LogLevel.Warning,
        Message = "GenerateEmp201: no finalized payroll run found for tenant={TenantId} period={Period}")]
    private static partial void LogNoFinalizedRunWarning(
        ILogger logger, string tenantId, string period);
}
