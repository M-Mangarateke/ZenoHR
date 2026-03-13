// REQ-OPS-001: Load benchmarks for payroll calculation engine.
// SLA: 500-employee payroll run must complete within 15 minutes.
// Uses BenchmarkDotNet to measure pure calculation throughput (no I/O).
// Firestore I/O is excluded from this benchmark (measured separately in load tests).

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Payroll.Calculation;
using ZenoHR.Module.Payroll.Entities;

namespace ZenoHR.Benchmarks;

/// <summary>
/// BenchmarkDotNet benchmarks for the payroll calculation engine.
/// Measures throughput for single-employee baselines and batch calculations
/// of 100 and 500 employees (sequential and parallel).
/// REQ-OPS-001: SLA target is 500 employees within 15 minutes (900,000 ms).
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 2, iterationCount: 5)]
public class PayrollCalculationBenchmarks
{
    // ── Shared state — initialised once in GlobalSetup ─────────────────────

    private SarsPayeRuleSet _payeRules = null!;
    private SarsUifSdlRuleSet _uifSdlRules = null!;
    private SarsEtiRuleSet _etiRules = null!;

    private IReadOnlyList<EmployeeDataFactory.TestEmployee> _employees100 = null!;
    private IReadOnlyList<EmployeeDataFactory.TestEmployee> _employees500 = null!;

    // Reference employee for single-employee baselines
    // Monthly salary R45,000 — falls into the 31% bracket
    private readonly MoneyZAR _benchSalary = new(45_000m);
    private const int BenchAge = 35;

    // Fixed calculation date for ETI tier determination
    private static readonly DateOnly CalcDate = new(2026, 2, 28);
    private static readonly DateOnly StartDate2YearsAgo = new(2024, 2, 28);   // ETI Tier 1
    private static readonly DateOnly StartDate14MonthsAgo = new(2024, 12, 31); // ETI Tier 2

    [Params(100, 500)]
    public int EmployeeCount;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _payeRules   = BenchmarkRuleSets.Paye;
        _uifSdlRules = BenchmarkRuleSets.UifSdl;
        _etiRules    = BenchmarkRuleSets.Eti;

        // Generate once — deterministic seed ensures reproducibility
        _employees100 = EmployeeDataFactory.Generate(100);
        _employees500 = EmployeeDataFactory.Generate(500);
    }

    // ── Benchmark 1: Single employee PAYE (baseline) ───────────────────────

    /// <summary>
    /// Baseline: calculates monthly PAYE for one employee.
    /// All other benchmarks are measured relative to this.
    /// REQ-OPS-001
    /// </summary>
    [Benchmark(Baseline = true)]
    public MoneyZAR SingleEmployeePaye()
        => PayeCalculationEngine.CalculateMonthlyPAYE(_benchSalary, BenchAge, _payeRules);

    // ── Benchmark 2: Full single-employee calculation ──────────────────────

    /// <summary>
    /// Full per-employee calculation: PAYE + UIF + SDL + ETI check + PayrollResult.Create.
    /// Represents the real work done per employee in a payroll run.
    /// REQ-OPS-001
    /// </summary>
    [Benchmark]
    public Result<PayrollResult> SingleEmployeeFullCalculation()
        => CalculateEmployee("bench-emp-0000", _benchSalary, BenchAge,
                             StartDate2YearsAgo, CalcDate, EtiTier.Tier1);

    // ── Benchmark 3: Batch — sequential ───────────────────────────────────

    /// <summary>
    /// Processes <see cref="EmployeeCount"/> employees sequentially.
    /// Establishes the sequential throughput baseline.
    /// REQ-OPS-001
    /// </summary>
    [Benchmark]
    public void BatchCalculation_Sequential()
    {
        var employees = EmployeeCount == 500 ? _employees500 : _employees100;
        foreach (var emp in employees)
        {
            var tier = emp.IsEtiEligible ? EtiTier.Tier1 : EtiTier.Ineligible;
            _ = CalculateEmployee(emp.EmployeeId, emp.MonthlySalary, emp.AgeYears,
                                  StartDate2YearsAgo, CalcDate, tier);
        }
    }

    // ── Benchmark 4: Batch — parallel ─────────────────────────────────────

    /// <summary>
    /// Processes <see cref="EmployeeCount"/> employees in parallel (DOP=4).
    /// Demonstrates throughput on multi-core hardware.
    /// REQ-OPS-001
    /// </summary>
    [Benchmark]
    public void BatchCalculation_Parallel()
    {
        var employees = EmployeeCount == 500 ? _employees500 : _employees100;

        Parallel.ForEach(
            employees,
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            emp =>
            {
                var tier = emp.IsEtiEligible ? EtiTier.Tier1 : EtiTier.Ineligible;
                _ = CalculateEmployee(emp.EmployeeId, emp.MonthlySalary, emp.AgeYears,
                                      StartDate2YearsAgo, CalcDate, tier);
            });
    }

    // ── Shared calculation helper ──────────────────────────────────────────

    /// <summary>
    /// Performs the full statutory calculation for one employee:
    /// PAYE → UIF (employee + employer) → SDL → ETI eligibility + amount → PayrollResult.Create.
    /// Mirrors the logic in the PayrollRunOrchestrator service (no I/O).
    /// REQ-OPS-001
    /// </summary>
    private Result<PayrollResult> CalculateEmployee(
        string employeeId,
        MoneyZAR monthlySalary,
        int age,
        DateOnly employmentStart,
        DateOnly calcDate,
        EtiTier tier)
    {
        // PAYE — annual equivalent method (PRD-16 Section 1)
        var paye = PayeCalculationEngine.CalculateMonthlyPAYE(monthlySalary, age, _payeRules);

        // UIF — employee and employer (PRD-16 Section 8)
        var uifEmployee = UifSdlCalculationEngine.CalculateUifEmployee(monthlySalary, _uifSdlRules);
        var uifEmployer = UifSdlCalculationEngine.CalculateUifEmployer(monthlySalary, _uifSdlRules);

        // SDL — employer-only; not exempt (Zenowethu annual payroll >> R500k)
        var sdl = UifSdlCalculationEngine.CalculateSdl(monthlySalary, _uifSdlRules, isEmployerSdlExempt: false);

        // ETI (PRD-16 Section 5)
        bool etiEligible = EtiCalculationEngine.IsEligible(age, monthlySalary, tier, _etiRules);
        var etiAmount = etiEligible
            ? EtiCalculationEngine.CalculateMonthlyEti(monthlySalary, tier, 160, _etiRules)
            : MoneyZAR.Zero;

        // Assemble result — no voluntary deductions in benchmark (simplest valid case)
        return PayrollResult.Create(
            employeeId: employeeId,
            payrollRunId: "bench-run-2026-02",
            tenantId: "bench-tenant-zenowethu",
            basicSalary: monthlySalary,
            overtimePay: MoneyZAR.Zero,
            allowances: MoneyZAR.Zero,
            paye: paye,
            uifEmployee: uifEmployee,
            uifEmployer: uifEmployer,
            sdl: sdl,
            pensionEmployee: MoneyZAR.Zero,
            pensionEmployer: MoneyZAR.Zero,
            medicalEmployee: MoneyZAR.Zero,
            medicalEmployer: MoneyZAR.Zero,
            etiAmount: etiAmount,
            etiEligible: etiEligible,
            otherDeductions: null,
            otherAdditions: null,
            hoursOrdinary: 160m,
            hoursOvertime: 0m,
            taxTableVersion: "2026.1.0",
            complianceFlags: null,
            calculationTimestamp: DateTimeOffset.UtcNow);
    }
}
