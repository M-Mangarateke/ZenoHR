// REQ-OPS-001: SLA validation harness for 500-employee payroll run.
// Run with: dotnet run --project tests/ZenoHR.Benchmarks -- load-test [count]
// Expected: completes in < 15 minutes (900,000ms).
// Firestore I/O excluded — measures pure calculation throughput only.

using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Payroll.Calculation;
using ZenoHR.Module.Payroll.Entities;

namespace ZenoHR.Benchmarks;

/// <summary>
/// Console harness that validates the 500-employee payroll SLA.
/// Uses wall-clock time (Stopwatch) to confirm the entire batch completes
/// within the 15-minute (900,000 ms) budget required by REQ-OPS-001.
/// Pure calculation path only — Firestore I/O is measured in integration tests.
/// REQ-OPS-001
/// </summary>
public static class LoadTestHarness
{
    private const long SlaBudgetMs = 900_000L; // 15 minutes

    private static readonly DateOnly CalcDate         = new(2026, 2, 28);
    private static readonly DateOnly EtiStartTier1    = new(2024, 2, 28); // 24 months ago → Tier1

    /// <summary>
    /// Runs the SLA validation for <paramref name="employeeCount"/> employees.
    /// Prints a structured report to stdout and exits with code 1 if SLA is missed.
    /// REQ-OPS-001
    /// </summary>
    public static int RunSlaValidation(int employeeCount = 500)
    {
        Console.WriteLine("=== ZenoHR Load Test: Payroll Calculation SLA Validation ===");
        Console.WriteLine($"  Tenant:       Zenowethu (Pty) Ltd");
        Console.WriteLine($"  Employees:    {employeeCount}");
        Console.WriteLine($"  SLA target:   < {SlaBudgetMs:N0} ms (15 minutes)");
        Console.WriteLine($"  Tax year:     2026 (2025/26 SARS brackets)");
        Console.WriteLine($"  Concurrency:  Parallel.ForEach (DOP=Environment.ProcessorCount)");
        Console.WriteLine();

        // ── Prepare rule sets ────────────────────────────────────────────────
        var payeRules   = BenchmarkRuleSets.Paye;
        var uifSdlRules = BenchmarkRuleSets.UifSdl;
        var etiRules    = BenchmarkRuleSets.Eti;

        // ── Generate employee profiles ────────────────────────────────────────
        Console.Write("  Generating employee profiles... ");
        var employees = EmployeeDataFactory.Generate(employeeCount, seed: 42);
        Console.WriteLine($"done ({employees.Count} employees, seed=42).");
        Console.WriteLine();

        // ── Show salary distribution ──────────────────────────────────────────
        int etiCount = employees.Count(e => e.IsEtiEligible);
        Console.WriteLine($"  ETI-eligible employees:  {etiCount} ({etiCount * 100.0 / employeeCount:F1}%)");
        Console.WriteLine($"  Min monthly salary:      R {employees.Min(e => e.MonthlySalary.Amount):N2}");
        Console.WriteLine($"  Max monthly salary:      R {employees.Max(e => e.MonthlySalary.Amount):N2}");
        Console.WriteLine($"  Mean monthly salary:     R {employees.Average(e => e.MonthlySalary.Amount):N2}");
        Console.WriteLine();

        // ── Warmup ───────────────────────────────────────────────────────────
        Console.Write("  Warming up (10 iterations)... ");
        for (int w = 0; w < 10; w++)
        {
            var sample = employees[w % employees.Count];
            _ = CalculateEmployee(
                sample, EtiTier.Tier1,
                payeRules, uifSdlRules, etiRules);
        }
        Console.WriteLine("done.");
        Console.WriteLine();

        // ── Main calculation run ──────────────────────────────────────────────
        Console.WriteLine("  Starting timed calculation run...");

        int successCount = 0;
        int failCount    = 0;
        decimal totalGross = 0m;
        decimal totalPaye  = 0m;
        decimal totalEti   = 0m;
        decimal totalUif   = 0m;
        decimal totalSdl   = 0m;

        // Thread-safe accumulators
        object lockObj = new();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        Parallel.ForEach(
            employees,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            emp =>
            {
                var tier = emp.IsEtiEligible ? EtiTier.Tier1 : EtiTier.Ineligible;
                var result = CalculateEmployee(emp, tier, payeRules, uifSdlRules, etiRules);

                if (result.IsSuccess)
                {
                    var pr = result.Value!;
                    lock (lockObj)
                    {
                        successCount++;
                        totalGross += pr.GrossPay.Amount;
                        totalPaye  += pr.Paye.Amount;
                        totalEti   += pr.EtiAmount.Amount;
                        totalUif   += pr.UifEmployee.Amount;
                        totalSdl   += pr.Sdl.Amount;
                    }
                }
                else
                {
                    lock (lockObj) { failCount++; }
                    Console.Error.WriteLine(
                        $"  [FAIL] {emp.EmployeeId}: {result.Error?.Message}");
                }
            });

        sw.Stop();

        // ── Report ────────────────────────────────────────────────────────────
        bool slaPassed = sw.ElapsedMilliseconds < SlaBudgetMs;

        Console.WriteLine();
        Console.WriteLine("  ─────────────────────────────────────────────────────────");
        Console.WriteLine($"  Employees processed:  {successCount}/{employeeCount}");
        Console.WriteLine($"  Failures:             {failCount}");
        Console.WriteLine();
        Console.WriteLine($"  Total gross pay:      R {totalGross:N2}");
        Console.WriteLine($"  Total PAYE:           R {totalPaye:N2}");
        Console.WriteLine($"  Total ETI:            R {totalEti:N2}");
        Console.WriteLine($"  Total UIF (employee): R {totalUif:N2}");
        Console.WriteLine($"  Total SDL (employer): R {totalSdl:N2}");
        Console.WriteLine($"  Effective PAYE rate:  {(totalGross > 0 ? totalPaye / totalGross * 100 : 0):F2}%");
        Console.WriteLine();
        Console.WriteLine($"  Elapsed:              {sw.Elapsed.TotalSeconds:F3}s ({sw.ElapsedMilliseconds:N0}ms)");
        Console.WriteLine($"  Throughput:           {employeeCount / sw.Elapsed.TotalSeconds:F1} employees/sec");
        Console.WriteLine($"  SLA target:           {SlaBudgetMs:N0}ms (15 minutes)");
        Console.WriteLine($"  SLA result:           {(slaPassed ? "PASS" : "FAIL")}");
        Console.WriteLine("  ─────────────────────────────────────────────────────────");
        Console.WriteLine();

        if (failCount > 0)
        {
            Console.Error.WriteLine($"  ERROR: {failCount} calculation(s) failed — check output above.");
            return 2;
        }

        if (!slaPassed)
        {
            Console.Error.WriteLine(
                $"  SLA VIOLATION: Run completed in {sw.ElapsedMilliseconds:N0}ms, " +
                $"exceeding budget of {SlaBudgetMs:N0}ms by {sw.ElapsedMilliseconds - SlaBudgetMs:N0}ms.");
            return 1;
        }

        Console.WriteLine("  All employees processed successfully within the 15-minute SLA.");
        return 0;
    }

    // ── Shared calculation helper ──────────────────────────────────────────

    /// <summary>
    /// Full per-employee statutory calculation (PAYE + UIF + SDL + ETI + PayrollResult.Create).
    /// Thread-safe: all rule sets are immutable read-only objects.
    /// REQ-OPS-001
    /// </summary>
    private static Result<PayrollResult> CalculateEmployee(
        EmployeeDataFactory.TestEmployee emp,
        EtiTier tier,
        SarsPayeRuleSet payeRules,
        SarsUifSdlRuleSet uifSdlRules,
        SarsEtiRuleSet etiRules)
    {
        var salary = emp.MonthlySalary;
        int age    = emp.AgeYears;

        // PAYE — annual equivalent method (PRD-16 Section 1)
        var paye = PayeCalculationEngine.CalculateMonthlyPAYE(salary, age, payeRules);

        // UIF — employee and employer sides (PRD-16 Section 8)
        var uifEmployee = UifSdlCalculationEngine.CalculateUifEmployee(salary, uifSdlRules);
        var uifEmployer = UifSdlCalculationEngine.CalculateUifEmployer(salary, uifSdlRules);

        // SDL — employer-only; Zenowethu is not SDL-exempt (PRD-16 Section 6)
        var sdl = UifSdlCalculationEngine.CalculateSdl(salary, uifSdlRules, isEmployerSdlExempt: false);

        // ETI (PRD-16 Section 5)
        bool etiEligible = EtiCalculationEngine.IsEligible(age, salary, tier, etiRules);
        var etiAmount = etiEligible
            ? EtiCalculationEngine.CalculateMonthlyEti(salary, tier, 160, etiRules)
            : MoneyZAR.Zero;

        return PayrollResult.Create(
            employeeId:            emp.EmployeeId,
            payrollRunId:          "load-run-2026-02",
            tenantId:              "bench-tenant-zenowethu",
            basicSalary:           salary,
            overtimePay:           MoneyZAR.Zero,
            allowances:            MoneyZAR.Zero,
            paye:                  paye,
            uifEmployee:           uifEmployee,
            uifEmployer:           uifEmployer,
            sdl:                   sdl,
            pensionEmployee:       MoneyZAR.Zero,
            pensionEmployer:       MoneyZAR.Zero,
            medicalEmployee:       MoneyZAR.Zero,
            medicalEmployer:       MoneyZAR.Zero,
            etiAmount:             etiAmount,
            etiEligible:           etiEligible,
            otherDeductions:       null,
            otherAdditions:        null,
            hoursOrdinary:         160m,
            hoursOvertime:         0m,
            taxTableVersion:       "2026.1.0",
            complianceFlags:       null,
            calculationTimestamp:  DateTimeOffset.UtcNow);
    }
}
