// REQ-HR-003, CTL-SARS-001: Payroll orchestration service.
// REQ-OPS-005: Structured diagnostic logging for every pipeline step — EventIds 3000-3006.
// TASK-085: Coordinates rule-set loading → per-employee calculation → PayrollResult creation → run finalization.
// Cross-module: reads Employee + EmploymentContract + EmployeeBenefit; writes PayrollRun + PayrollResults.
// CTL-SARS-001: All statutory rates loaded from StatutoryRuleSetRepository (never hardcoded).

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Module.Employee.Aggregates;
using ZenoHR.Module.Payroll.Aggregates;
using ZenoHR.Module.Payroll.Calculation;
using ZenoHR.Module.Payroll.Entities;

namespace ZenoHR.Infrastructure.Services;

/// <summary>
/// Orchestrates a complete payroll run for a set of employees.
/// <para>
/// Pipeline: load rule-sets → per-employee calculation → aggregate totals →
/// create PayrollResults → MarkCalculated → compute SHA-256 checksum → Finalize.
/// </para>
/// REQ-HR-003: Full PAYE/UIF/SDL/ETI calculation per employee per period.
/// CTL-SARS-001: Rule-sets loaded from Firestore — no hardcoded rates.
/// </summary>
public sealed partial class PayrollOrchestrationService
{
    private readonly StatutoryRuleSetRepository _ruleSetRepo;
    private readonly EmployeeRepository _employeeRepo;
    private readonly EmploymentContractRepository _contractRepo;
    private readonly EmployeeBenefitRepository _benefitRepo;
    private readonly PayrollRunRepository _runRepo;
    private readonly PayrollResultRepository _resultRepo;
    private readonly ILogger<PayrollOrchestrationService> _logger;

    public PayrollOrchestrationService(
        StatutoryRuleSetRepository ruleSetRepo,
        EmployeeRepository employeeRepo,
        EmploymentContractRepository contractRepo,
        EmployeeBenefitRepository benefitRepo,
        PayrollRunRepository runRepo,
        PayrollResultRepository resultRepo,
        ILogger<PayrollOrchestrationService> logger)
    {
        ArgumentNullException.ThrowIfNull(ruleSetRepo);
        ArgumentNullException.ThrowIfNull(employeeRepo);
        ArgumentNullException.ThrowIfNull(contractRepo);
        ArgumentNullException.ThrowIfNull(benefitRepo);
        ArgumentNullException.ThrowIfNull(runRepo);
        ArgumentNullException.ThrowIfNull(resultRepo);
        ArgumentNullException.ThrowIfNull(logger);
        _ruleSetRepo = ruleSetRepo;
        _employeeRepo = employeeRepo;
        _contractRepo = contractRepo;
        _benefitRepo = benefitRepo;
        _runRepo = runRepo;
        _resultRepo = resultRepo;
        _logger = logger;
    }

    // ── Main entry point ─────────────────────────────────────────────────────

    /// <summary>
    /// Creates a payroll run, calculates all employee payslips, and returns the run in
    /// <see cref="PayrollRunStatus.Calculated"/> state ready for Director/HRManager review.
    /// </summary>
    /// <param name="tenantId">Tenant isolation key.</param>
    /// <param name="period">Period identifier: <c>"YYYY-MM"</c> (monthly) or <c>"YYYY-WNN"</c> (weekly).</param>
    /// <param name="runType">Monthly or Weekly.</param>
    /// <param name="employeeIds">Employee IDs to include in this run.</param>
    /// <param name="ruleSetVersion">Statutory rule set version string (from active StatutoryRuleSet).</param>
    /// <param name="initiatedBy">Firebase UID of the creating Director or HRManager.</param>
    /// <param name="idempotencyKey">Idempotency token — prevents duplicate runs on retries.</param>
    /// <param name="isSdlExempt">
    /// True if the employer's annual leviable payroll is below the SDL exemption threshold.
    /// This is a tenant-level configuration, not computed per-run.
    /// </param>
    /// <param name="now">Current UTC timestamp.</param>
    public async Task<Result<PayrollRun>> RunPayrollAsync(
        string tenantId,
        string period,
        PayFrequency runType,
        IReadOnlyList<string> employeeIds,
        string ruleSetVersion,
        string initiatedBy,
        string idempotencyKey,
        bool isSdlExempt,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        LogRunStarting(tenantId, period, runType, employeeIds.Count);

        // ── 1. Create the PayrollRun in Draft ───────────────────────────────
        var runId = $"pr_{now.Year:D4}_{now.Month:D2}_{idempotencyKey[..8]}";
        var createResult = PayrollRun.Create(
            id: runId,
            tenantId: tenantId,
            period: period,
            runType: runType,
            employeeIds: employeeIds,
            ruleSetVersion: ruleSetVersion,
            initiatedBy: initiatedBy,
            idempotencyKey: idempotencyKey,
            now: now);

        if (createResult.IsFailure)
        {
            LogRunAborted(runId, "CreatePayrollRun", createResult.Error!.Code);
            return createResult;
        }
        var run = createResult.Value!;

        // ── 2. Load statutory rule sets ────────────────────────────────────
        var periodEndDate = RunPeriodEndDate(period, runType, now);

        var payeRuleSetResult = await _ruleSetRepo.GetEffectiveAsync(
            RuleDomains.SarsPaye, periodEndDate, ct);
        if (payeRuleSetResult.IsFailure)
        {
            LogRunAborted(runId, "LoadPayeRuleSet", payeRuleSetResult.Error!.Code);
            return Result<PayrollRun>.Failure(payeRuleSetResult.Error!);
        }

        var uifSdlRuleSetResult = await _ruleSetRepo.GetEffectiveAsync(
            RuleDomains.SarsUifSdl, periodEndDate, ct);
        if (uifSdlRuleSetResult.IsFailure)
        {
            LogRunAborted(runId, "LoadUifSdlRuleSet", uifSdlRuleSetResult.Error!.Code);
            return Result<PayrollRun>.Failure(uifSdlRuleSetResult.Error!);
        }

        var etiRuleSetResult = await _ruleSetRepo.GetEffectiveAsync(
            RuleDomains.SarsEti, periodEndDate, ct);
        if (etiRuleSetResult.IsFailure)
        {
            LogRunAborted(runId, "LoadEtiRuleSet", etiRuleSetResult.Error!.Code);
            return Result<PayrollRun>.Failure(etiRuleSetResult.Error!);
        }

        var payeRules = SarsPayeRuleSet.From(payeRuleSetResult.Value!);
        var uifSdlRules = SarsUifSdlRuleSet.From(uifSdlRuleSetResult.Value!);
        var etiRules = SarsEtiRuleSet.From(etiRuleSetResult.Value!);
        var taxTableVersion = payeRuleSetResult.Value!.Version;
        LogRuleSetLoaded(taxTableVersion);

        // ── 3. Calculate each employee ─────────────────────────────────────
        var results = new List<PayrollResult>(employeeIds.Count);
        var complianceFlags = new List<string>();
        var empIndex = 0;
        var skipCount = 0;

        foreach (var empId in employeeIds)
        {
            empIndex++;
            LogCalculatingEmployee(empIndex, employeeIds.Count, empId);

            var empResult = await _employeeRepo.GetByEmployeeIdAsync(tenantId, empId, ct);
            if (empResult.IsFailure)
            {
                LogEmployeeSkipped(empId, empResult.Error!.Code);
                complianceFlags.Add($"WARN:employee_{empId}_not_found");
                skipCount++;
                continue;
            }

            var emp = empResult.Value!;
            var contractResult = await _contractRepo.GetActiveContractAsync(tenantId, empId, ct);
            if (contractResult.IsFailure)
            {
                LogEmployeeSkipped(empId, contractResult.Error!.Code);
                complianceFlags.Add($"WARN:contract_missing_{empId}");
                skipCount++;
                continue;
            }

            var contract = contractResult.Value!;
            var benefits = await _benefitRepo.ListActiveByEmployeeAsync(tenantId, empId, ct);

            var calcResult = CalculateEmployee(
                emp, contract, benefits, runType, periodEndDate,
                payeRules, uifSdlRules, etiRules, isSdlExempt,
                runId, tenantId, taxTableVersion, now);

            if (calcResult.IsFailure)
            {
                LogEmployeeSkipped(empId, calcResult.Error!.Code);
                complianceFlags.Add($"FAIL:calc_error_{empId}:{calcResult.Error!.Message}");
                skipCount++;
                continue;
            }

            results.Add(calcResult.Value!);
        }

        // CTL-SARS-001: Fail if no valid results produced
        if (results.Count == 0)
        {
            LogRunAborted(runId, "CalculateEmployees", ZenoHrErrorCode.PayrollCalculationFailed);
            return Result<PayrollRun>.Failure(ZenoHrErrorCode.PayrollCalculationFailed,
                "Payroll calculation produced no results. Check employee contracts and compliance flags.");
        }

        // ── 4. Aggregate totals ────────────────────────────────────────────
        var grossTotal = results.Aggregate(MoneyZAR.Zero, (acc, r) => acc + r.GrossPay);
        var payeTotal = results.Aggregate(MoneyZAR.Zero, (acc, r) => acc + r.Paye);
        var uifTotal = results.Aggregate(MoneyZAR.Zero,
            (acc, r) => acc + r.UifEmployee + r.UifEmployer);
        var sdlTotal = results.Aggregate(MoneyZAR.Zero, (acc, r) => acc + r.Sdl);
        var etiTotal = results.Aggregate(MoneyZAR.Zero, (acc, r) => acc + r.EtiAmount);
        var deductionTotal = results.Aggregate(MoneyZAR.Zero, (acc, r) => acc + r.DeductionTotal);
        var netTotal = results.Aggregate(MoneyZAR.Zero, (acc, r) => acc + r.NetPay);

        // CTL-SARS-001 compliance flag
        complianceFlags.Add("CTL-SARS-001:PASS");

        // ── 5. Mark run as Calculated ─────────────────────────────────────
        var calcMark = run.MarkCalculated(
            grossTotal, payeTotal, uifTotal, sdlTotal, etiTotal,
            deductionTotal, netTotal, complianceFlags, initiatedBy, now);
        if (calcMark.IsFailure) return calcMark;

        // ── 6. Persist results then run ────────────────────────────────────
        var batchResult = await _resultRepo.CreateBatchAsync(results, ct);
        if (batchResult.IsFailure) return Result<PayrollRun>.Failure(batchResult.Error!);

        var saveResult = await _runRepo.SaveAsync(run, ct);
        if (saveResult.IsFailure) return Result<PayrollRun>.Failure(saveResult.Error!);

        sw.Stop();
        LogRunComplete(runId, results.Count, skipCount, sw.ElapsedMilliseconds);
        return Result<PayrollRun>.Success(run);
    }

    /// <summary>
    /// Finalizes a Calculated payroll run — makes it immutable and ready for EMP201 filing.
    /// Computes SHA-256 checksum over sorted payroll result payloads.
    /// </summary>
    public async Task<Result<PayrollRun>> FinalizeRunAsync(
        string tenantId, string runId, string finalizedBy, DateTimeOffset now, CancellationToken ct)
    {
        var runResult = await _runRepo.GetByRunIdAsync(tenantId, runId, ct);
        if (runResult.IsFailure) return runResult;

        var run = runResult.Value!;

        // Compute checksum over sorted results + rule set version
        var results = await _resultRepo.ListByRunAsync(runId, ct);
        var checksum = ComputeChecksum(results, run.RuleSetVersion);

        var finalizeResult = run.Finalize(checksum, finalizedBy, now);
        if (finalizeResult.IsFailure)
        {
            LogRunAborted(runId, "Finalize", finalizeResult.Error!.Code);
            return finalizeResult;
        }

        var saveResult = await _runRepo.SaveAsync(run, ct);
        if (saveResult.IsFailure) return Result<PayrollRun>.Failure(saveResult.Error!);

        LogRunFinalized(runId);
        return Result<PayrollRun>.Success(run);
    }

    // ── Per-employee calculation ─────────────────────────────────────────────

    private static Result<PayrollResult> CalculateEmployee(
        Employee emp,
        EmploymentContract contract,
        IReadOnlyList<EmployeeBenefit> benefits,
        PayFrequency runType,
        DateOnly periodEndDate,
        SarsPayeRuleSet payeRules,
        SarsUifSdlRuleSet uifSdlRules,
        SarsEtiRuleSet etiRules,
        bool isSdlExempt,
        string runId,
        string tenantId,
        string taxTableVersion,
        DateTimeOffset now)
    {
        // ── Gross components ──────────────────────────────────────────────
        var basicSalary = contract.SalaryBasis == SalaryBasis.Monthly
            ? contract.BaseSalary
            : contract.BaseSalary * (contract.OrdinaryHoursPerWeek * 52m / 12m);

        var overtimePay = MoneyZAR.Zero;  // Populated from timesheets in TASK-086+
        var allowances = MoneyZAR.Zero;   // Contract allowances — future phase

        // ── Hours (standard; actual timesheets in later phase) ────────────
        var hoursOrdinary = (decimal)(contract.OrdinaryHoursPerWeek * (runType == PayFrequency.Weekly ? 1m : 52m / 12m));
        var hoursOvertime = 0m;

        // ── PAYE ──────────────────────────────────────────────────────────
        int age = CalculateAge(emp.DateOfBirth, periodEndDate);
        var paye = runType == PayFrequency.Monthly
            ? PayeCalculationEngine.CalculateMonthlyPAYE(basicSalary, age, payeRules)
            : PayeCalculationEngine.CalculateWeeklyPAYE(basicSalary, age, payeRules);

        // ── UIF ───────────────────────────────────────────────────────────
        var uifEmployee = runType == PayFrequency.Monthly
            ? UifSdlCalculationEngine.CalculateUifEmployee(basicSalary, uifSdlRules)
            : UifSdlCalculationEngine.CalculateUifEmployeeWeekly(basicSalary, uifSdlRules);
        var uifEmployer = runType == PayFrequency.Monthly
            ? UifSdlCalculationEngine.CalculateUifEmployer(basicSalary, uifSdlRules)
            : UifSdlCalculationEngine.CalculateUifEmployerWeekly(basicSalary, uifSdlRules);

        // ── SDL ───────────────────────────────────────────────────────────
        var sdl = UifSdlCalculationEngine.CalculateSdl(basicSalary, uifSdlRules, isSdlExempt);

        // ── ETI ───────────────────────────────────────────────────────────
        var etiTier = EtiCalculationEngine.GetTier(contract.StartDate, periodEndDate);
        var etiEligible = EtiCalculationEngine.IsEligible(age, basicSalary, etiTier, etiRules);
        var etiAmount = etiEligible
            ? EtiCalculationEngine.CalculateMonthlyEti(basicSalary, etiTier, (int)hoursOrdinary, etiRules)
            : MoneyZAR.Zero;

        // ── Pension & Medical (from active benefits) ───────────────────────
        var pensionEmployee = MoneyZAR.Zero;
        var pensionEmployer = MoneyZAR.Zero;
        var medicalEmployee = MoneyZAR.Zero;
        var medicalEmployer = MoneyZAR.Zero;

        foreach (var benefit in benefits)
        {
            switch (benefit.BenefitType)
            {
                case BenefitType.PensionFund:
                case BenefitType.ProvidentFund:
                    pensionEmployee += basicSalary * benefit.EmployeeContributionRate;
                    pensionEmployer += basicSalary * benefit.EmployerContributionRate;
                    break;

                case BenefitType.MedicalAid:
                    medicalEmployee += basicSalary * benefit.EmployeeContributionRate;
                    medicalEmployer += basicSalary * benefit.EmployerContributionRate;
                    break;
            }
        }

        // Round benefit contributions to cent
        pensionEmployee = pensionEmployee.RoundToCent();
        pensionEmployer = pensionEmployer.RoundToCent();
        medicalEmployee = medicalEmployee.RoundToCent();
        medicalEmployer = medicalEmployer.RoundToCent();

        // ── Compliance flags ──────────────────────────────────────────────
        var flags = new List<string> { "CTL-SARS-001:PASS" };
        if (etiEligible) flags.Add("CTL-SARS-003:ETI-ELIGIBLE");

        return PayrollResult.Create(
            employeeId: emp.EmployeeId,
            payrollRunId: runId,
            tenantId: tenantId,
            basicSalary: basicSalary,
            overtimePay: overtimePay,
            allowances: allowances,
            paye: paye,
            uifEmployee: uifEmployee,
            uifEmployer: uifEmployer,
            sdl: sdl,
            pensionEmployee: pensionEmployee,
            pensionEmployer: pensionEmployer,
            medicalEmployee: medicalEmployee,
            medicalEmployer: medicalEmployer,
            etiAmount: etiAmount,
            etiEligible: etiEligible,
            otherDeductions: null,
            otherAdditions: null,
            hoursOrdinary: hoursOrdinary,
            hoursOvertime: hoursOvertime,
            taxTableVersion: taxTableVersion,
            complianceFlags: flags,
            calculationTimestamp: now);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static int CalculateAge(DateOnly dateOfBirth, DateOnly asOf)
    {
        var age = asOf.Year - dateOfBirth.Year;
        if (asOf < dateOfBirth.AddYears(age)) age--;
        return age;
    }

    private static DateOnly RunPeriodEndDate(string period, PayFrequency runType, DateTimeOffset fallback)
    {
        // Monthly: "YYYY-MM" → last day of that month
        if (runType == PayFrequency.Monthly
            && DateOnly.TryParseExact(period + "-01", "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var first))
        {
            return new DateOnly(first.Year, first.Month,
                DateTime.DaysInMonth(first.Year, first.Month));
        }

        // Weekly: "YYYY-WNN" — use fallback (current date)
        return DateOnly.FromDateTime(fallback.UtcDateTime);
    }

    /// <summary>
    /// Computes SHA-256 checksum over sorted payroll result payloads + rule set version.
    /// The sort order is deterministic (employee ID ascending) so the checksum is reproducible.
    /// CTL-SARS-001: This checksum is stored on the Finalized PayrollRun for integrity verification.
    /// </summary>
    private static string ComputeChecksum(IReadOnlyList<PayrollResult> results, string ruleSetVersion)
    {
        var sortedPayloads = results
            .OrderBy(r => r.EmployeeId, StringComparer.Ordinal)
            .Select(r => new
            {
                r.EmployeeId,
                gross = r.GrossPay.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                paye = r.Paye.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                net = r.NetPay.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            })
            .ToList();

        var json = JsonSerializer.Serialize(new { ruleSetVersion, payloads = sortedPayloads });
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // ── Diagnostic logging (source-generated, zero-allocation) ───────────────

    [LoggerMessage(EventId = 3000, Level = LogLevel.Information,
        Message = "PayrollRun starting TenantId={TenantId} Period={Period} RunType={RunType} Employees={Count}")]
    private partial void LogRunStarting(string tenantId, string period, PayFrequency runType, int count);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Debug,
        Message = "RuleSet loaded Version={Version}")]
    private partial void LogRuleSetLoaded(string version);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Debug,
        Message = "Calculating employee {Index}/{Total} EmployeeId={EmployeeId}")]
    private partial void LogCalculatingEmployee(int index, int total, string employeeId);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Warning,
        Message = "Employee calculation skipped EmployeeId={EmployeeId} ErrorCode={ErrorCode}")]
    private partial void LogEmployeeSkipped(string employeeId, ZenoHrErrorCode errorCode);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Information,
        Message = "PayrollRun complete RunId={RunId} Success={Success} Skipped={Skipped} ElapsedMs={ElapsedMs}")]
    private partial void LogRunComplete(string runId, int success, int skipped, long elapsedMs);

    [LoggerMessage(EventId = 3005, Level = LogLevel.Information,
        Message = "PayrollRun finalized RunId={RunId}")]
    private partial void LogRunFinalized(string runId);

    [LoggerMessage(EventId = 3006, Level = LogLevel.Error,
        Message = "PayrollRun aborted RunId={RunId} Step={Step} ErrorCode={ErrorCode}")]
    private partial void LogRunAborted(string runId, string step, ZenoHrErrorCode errorCode);
}
