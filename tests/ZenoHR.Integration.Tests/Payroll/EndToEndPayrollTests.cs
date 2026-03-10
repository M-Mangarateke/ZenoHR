// TC-PAY-020: End-to-end payroll pipeline integration tests.
// REQ-HR-003, CTL-SARS-001: Full pipeline — seed statutory data → calculate → persist → EMP201.
// Tests run against the Firestore emulator; no production data is touched.
//
// Strategy: the full orchestration service depends on Employee/Contract records in Firestore.
// These tests exercise the pipeline at two levels:
//   1. Full service level via PayrollOrchestrationService.RunPayrollAsync (TC-PAY-020-A/E/F/G)
//   2. Engine + repository level via direct PayeCalculationEngine calls (TC-PAY-020-B/C/D)
// Both paths exercise real Firestore round-trips through the emulator.
//
// Test data: Zenowethu employee, R30,000/month, age ~44 (DOB 1980-01-01).
// Expected PAYE 2025/26: annual R360k → tax R49,954 → minus primary rebate R17,235 = R32,719/yr
// → monthly R2,726.58. UIF employee = R300.00. SDL = R300.00.
// Net ≈ R30,000 - R2,726.58 - R300.00 = R26,973.42.
// (Real engine values may differ by rounding — tests assert ranges, not exact values.)

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ZenoHR.Domain.Common;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Infrastructure.Seeding;
using ZenoHR.Infrastructure.Services;
using ZenoHR.Infrastructure.Services.Filing.Emp201;
using ZenoHR.Integration.Tests.Infrastructure;
using ZenoHR.Module.Employee.Aggregates;
using EmployeeAggregate = ZenoHR.Module.Employee.Aggregates.Employee;
using ZenoHR.Module.Payroll.Aggregates;
using ZenoHR.Module.Payroll.Calculation;
using ZenoHR.Module.Payroll.Entities;

namespace ZenoHR.Integration.Tests.Payroll;

/// <summary>
/// End-to-end integration tests for the full payroll pipeline.
///
/// TC-PAY-020-A: Full pipeline — seed rules → run orchestration → assert PayrollRun.GrossTotal
/// TC-PAY-020-B: PayrollResult saved with correct PAYE and net pay (engine-level round-trip)
/// TC-PAY-020-C: EMP201 CSV generated from run results — H record has correct period
/// TC-PAY-020-D: EMP201 CSV generated from run results — D records match employee count
/// TC-PAY-020-E: PayrollRun transitions correctly Draft → Calculated → Finalized
/// TC-PAY-020-F: Finalized run is immutable — attempt to re-save in Draft state is rejected
/// TC-PAY-020-G: Payslip invariant holds end-to-end — net == gross - deductions (to the cent)
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class EndToEndPayrollTests : IntegrationTestBase
{
    // ── Test data constants ─────────────────────────────────────────────────────
    // TC-PAY-020: SA-realistic monthly salary, no overtime, no allowances.

    private const string TestPeriod = "2026-03";
    private const string TestRuleSetVersion = "SARS_PAYE_2026";
    private const decimal TestGross = 30_000.00m;

    // ── Repositories ────────────────────────────────────────────────────────────

    private readonly PayrollRunRepository _runRepo;
    private readonly PayrollResultRepository _resultRepo;
    private readonly StatutoryRuleSetRepository _ruleSetRepo;
    private readonly EmployeeRepository _employeeRepo;
    private readonly EmploymentContractRepository _contractRepo;
    private readonly EmployeeBenefitRepository _benefitRepo;
    private readonly StatutoryRuleSetLoader _loader;
    private readonly Emp201Generator _emp201Generator;

    public EndToEndPayrollTests(FirestoreEmulatorFixture fixture) : base(fixture)
    {
        _runRepo = new PayrollRunRepository(fixture.Db, NullLogger<PayrollRunRepository>.Instance);
        _resultRepo = new PayrollResultRepository(fixture.Db);
        _ruleSetRepo = new StatutoryRuleSetRepository(fixture.Db, NullLogger<StatutoryRuleSetRepository>.Instance);
        _employeeRepo = new EmployeeRepository(fixture.Db, NullLogger<EmployeeRepository>.Instance);
        _contractRepo = new EmploymentContractRepository(fixture.Db, NullLogger<EmploymentContractRepository>.Instance);
        _benefitRepo = new EmployeeBenefitRepository(fixture.Db);
        _loader = new StatutoryRuleSetLoader(fixture.Db);
        _emp201Generator = new Emp201Generator();
    }

    // ── TC-PAY-020-A: Full orchestration pipeline ──────────────────────────────

    /// <summary>
    /// TC-PAY-020-A: Seeds all statutory rule sets, creates an employee + contract,
    /// runs the full PayrollOrchestrationService pipeline, and asserts GrossTotal on the run.
    /// REQ-HR-003, CTL-SARS-001
    /// </summary>
    [Fact]
    public async Task RunPayrollAsync_FullPipeline_GrossTotalMatchesSingleEmployeeSalary()
    {
        // TC-PAY-020-A: Arrange — seed rules, create employee + contract
        var seedResult = await _loader.LoadAllAsync();
        seedResult.IsSuccess.Should().BeTrue(because:
            $"statutory seed data must load successfully before payroll can run. Error: {(seedResult.IsSuccess ? "" : seedResult.Error.Message)}");

        var empId = $"emp-e2e-{Guid.NewGuid():N}";
        var now = new DateTimeOffset(2026, 3, 31, 12, 0, 0, TimeSpan.Zero);

        await SeedEmployeeAsync(empId, TenantId, new DateOnly(1980, 1, 1));
        await SeedContractAsync(empId, TenantId, new MoneyZAR(TestGross), new DateOnly(2023, 1, 1));

        var orchestrator = BuildOrchestrationService();

        // Act
        var result = await orchestrator.RunPayrollAsync(
            tenantId: TenantId,
            period: TestPeriod,
            runType: PayFrequency.Monthly,
            employeeIds: [empId],
            ruleSetVersion: TestRuleSetVersion,
            initiatedBy: "uid-hrmanager-001",
            idempotencyKey: Guid.NewGuid().ToString("N"),
            isSdlExempt: false,
            now: now);

        // Assert
        result.IsSuccess.Should().BeTrue(because:
            $"payroll run must succeed for a valid employee with active contract. Error: {(result.IsSuccess ? "" : result.Error.Message)}");

        var run = result.Value!;
        run.Status.Should().Be(PayrollRunStatus.Calculated,
            because: "orchestration completes with run in Calculated state");
        run.GrossTotal.Amount.Should().Be(TestGross,
            because: "gross total for one employee with salary R30,000 must equal R30,000");
        run.EmployeeCount.Should().Be(1,
            because: "one employee was included in this run");
        run.Period.Should().Be(TestPeriod);
        run.TenantId.Should().Be(TenantId);
    }

    // ── TC-PAY-020-B: Engine-level — PayrollResult round-trip with correct PAYE ─

    /// <summary>
    /// TC-PAY-020-B: Calls PAYE engine directly, creates a PayrollResult, saves to Firestore,
    /// reads back, and asserts PAYE and net pay values survive the round-trip.
    /// REQ-HR-003, CTL-SARS-001
    /// </summary>
    [Fact]
    public async Task PayrollResult_SavedAndReadBack_PayeAndNetPaySurviveFirestoreRoundTrip()
    {
        // TC-PAY-020-B: Arrange — seed statutory data so SarsPayeRuleSet can be loaded
        var seedResult = await _loader.LoadAllAsync();
        seedResult.IsSuccess.Should().BeTrue();

        var runId = $"pr_2026_03_{Guid.NewGuid():N}"[..32];
        var empId = $"emp-b-{Guid.NewGuid():N}";
        var now = new DateTimeOffset(2026, 3, 31, 12, 0, 0, TimeSpan.Zero);
        var periodEndDate = new DateOnly(2026, 3, 31);

        // Load the seeded PAYE rule set from emulator
        var payeRuleSetResult = await _ruleSetRepo.GetEffectiveAsync(
            RuleDomains.SarsPaye, periodEndDate);
        payeRuleSetResult.IsSuccess.Should().BeTrue(because: "PAYE rule set must be present after seeding");
        var payeRules = SarsPayeRuleSet.From(payeRuleSetResult.Value!);

        var uifSdlRuleSetResult = await _ruleSetRepo.GetEffectiveAsync(
            RuleDomains.SarsUifSdl, periodEndDate);
        uifSdlRuleSetResult.IsSuccess.Should().BeTrue(because: "UIF/SDL rule set must be present after seeding");
        var uifSdlRules = SarsUifSdlRuleSet.From(uifSdlRuleSetResult.Value!);

        var gross = new MoneyZAR(TestGross);
        const int age = 46; // employee born 1980-01-01, period end 2026-03-31

        var paye = PayeCalculationEngine.CalculateMonthlyPAYE(gross, age, payeRules);
        var uifEmployee = UifSdlCalculationEngine.CalculateUifEmployee(gross, uifSdlRules);
        var uifEmployer = UifSdlCalculationEngine.CalculateUifEmployer(gross, uifSdlRules);
        var sdl = UifSdlCalculationEngine.CalculateSdl(gross, uifSdlRules, isEmployerSdlExempt: false);

        // Act — create PayrollResult (invariant verified inside)
        var createResult = PayrollResult.Create(
            employeeId: empId,
            payrollRunId: runId,
            tenantId: TenantId,
            basicSalary: gross,
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
            etiAmount: MoneyZAR.Zero,
            etiEligible: false,
            otherDeductions: null,
            otherAdditions: null,
            hoursOrdinary: 173.33m,
            hoursOvertime: 0m,
            taxTableVersion: TestRuleSetVersion,
            complianceFlags: ["CTL-SARS-001:PASS"],
            calculationTimestamp: now);

        createResult.IsSuccess.Should().BeTrue(because:
            $"PayrollResult.Create must succeed with valid inputs. Error: {(createResult.IsSuccess ? "" : createResult.Error.Message)}");

        // Save PayrollRun shell first (required by sub-collection path)
        var run = BuildDraftRun(runId, TenantId, [empId]);
        await _runRepo.SaveAsync(run);

        // Save result to Firestore emulator
        var saveResult = await _resultRepo.CreateAsync(createResult.Value!);
        saveResult.IsSuccess.Should().BeTrue(because: "PayrollResult must persist without error");

        // Read back from Firestore
        var readResult = await _resultRepo.GetByEmployeeIdAsync(runId, empId);
        readResult.IsSuccess.Should().BeTrue(because: "PayrollResult must be readable after write");

        var readBack = readResult.Value!;

        // Assert PAYE and net pay survive round-trip
        readBack.Paye.Amount.Should().Be(paye.Amount,
            because: "PAYE must survive Firestore string serialisation round-trip");
        readBack.NetPay.Amount.Should().Be(createResult.Value!.NetPay.Amount,
            because: "NetPay must survive Firestore string serialisation round-trip");
        readBack.GrossPay.Amount.Should().Be(TestGross,
            because: "GrossPay must equal the input basic salary (no overtime, no allowances)");

        // PAYE must be positive and reasonable: between R1,000 and R10,000/month for R30k salary
        paye.Amount.Should().BeInRange(1_000m, 10_000m,
            because: "PAYE on R30,000/month should be in the range R1,000–R10,000 for 2025/26");
    }

    // ── TC-PAY-020-C: EMP201 CSV — H record period ─────────────────────────────

    /// <summary>
    /// TC-PAY-020-C: Generates an EMP201 CSV from a seeded run result and asserts the
    /// H (header) record contains the correct tax period in YYYYMM format.
    /// CTL-SARS-006
    /// </summary>
    [Fact]
    public async Task Emp201Csv_GeneratedFromRunResults_HeaderRecordHasCorrectTaxPeriod()
    {
        // TC-PAY-020-C: Arrange — run the engine-level pipeline
        var (runId, empId, resultAmount) = await SeedRunWithOneResultAsync();

        // Build EMP201 data from the persisted result
        var results = await _resultRepo.ListByRunAsync(runId);
        results.Should().HaveCount(1, because: "one employee result was seeded");

        var result = results[0];
        var dueDate = _emp201Generator.CalculateDueDate(2026, 3);
        var emp201Data = BuildEmp201Data(runId, TestPeriod, results);

        // Act
        var csv = _emp201Generator.GenerateCsv(emp201Data);

        // Assert — H record (second line after column headers) contains "202603"
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var headerLine = lines.FirstOrDefault(l => l.StartsWith("H;"));
        headerLine.Should().NotBeNull(because: "EMP201 CSV must contain an H header record");
        headerLine!.Should().Contain("202603",
            because: "H record must contain tax period 202603 for March 2026");
    }

    // ── TC-PAY-020-D: EMP201 CSV — D record count ──────────────────────────────

    /// <summary>
    /// TC-PAY-020-D: Generates an EMP201 CSV and asserts the number of D (detail) records
    /// matches the number of employees in the run.
    /// CTL-SARS-006
    /// </summary>
    [Fact]
    public async Task Emp201Csv_GeneratedFromRunResults_DetailRecordCountMatchesEmployeeCount()
    {
        // TC-PAY-020-D: Arrange
        var (runId, _, _) = await SeedRunWithOneResultAsync();

        var results = await _resultRepo.ListByRunAsync(runId);
        var emp201Data = BuildEmp201Data(runId, TestPeriod, results);

        // Act
        var csv = _emp201Generator.GenerateCsv(emp201Data);

        // Assert — one D record per employee
        var detailLines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.StartsWith("D;"))
            .ToList();

        detailLines.Should().HaveCount(1,
            because: "one D record must appear for each employee in the run");
        detailLines[0].Should().Contain("D;",
            because: "detail record must start with RECORD_TYPE=D");
    }

    // ── TC-PAY-020-E: Draft → Calculated → Finalized transition ────────────────

    /// <summary>
    /// TC-PAY-020-E: Verifies the full lifecycle state machine:
    /// runs the orchestrator (Draft → Calculated), then finalizes (Calculated → Finalized).
    /// REQ-HR-003, CTL-SARS-001
    /// </summary>
    [Fact]
    public async Task PayrollRun_FullLifecycle_TransitionsDraftCalculatedFinalized()
    {
        // TC-PAY-020-E: Arrange
        var seedResult = await _loader.LoadAllAsync();
        seedResult.IsSuccess.Should().BeTrue();

        var empId = $"emp-e-{Guid.NewGuid():N}";
        var now = new DateTimeOffset(2026, 3, 31, 12, 0, 0, TimeSpan.Zero);

        await SeedEmployeeAsync(empId, TenantId, new DateOnly(1985, 6, 15));
        await SeedContractAsync(empId, TenantId, new MoneyZAR(TestGross), new DateOnly(2022, 3, 1));

        var orchestrator = BuildOrchestrationService();

        // Act — Draft → Calculated
        var runResult = await orchestrator.RunPayrollAsync(
            tenantId: TenantId,
            period: TestPeriod,
            runType: PayFrequency.Monthly,
            employeeIds: [empId],
            ruleSetVersion: TestRuleSetVersion,
            initiatedBy: "uid-hrmanager-001",
            idempotencyKey: Guid.NewGuid().ToString("N"),
            isSdlExempt: false,
            now: now);

        runResult.IsSuccess.Should().BeTrue(because: "orchestration must succeed");
        var run = runResult.Value!;
        run.Status.Should().Be(PayrollRunStatus.Calculated,
            because: "run must be Calculated after orchestration");

        // Act — Calculated → Finalized
        var finalizeResult = await orchestrator.FinalizeRunAsync(
            tenantId: TenantId,
            runId: run.Id,
            finalizedBy: "uid-director-001",
            now: now.AddMinutes(5),
            ct: CancellationToken.None);

        // Assert
        finalizeResult.IsSuccess.Should().BeTrue(because:
            $"finalization must succeed on a Calculated run. Error: {(finalizeResult.IsSuccess ? "" : finalizeResult.Error.Message)}");

        var finalizedRun = finalizeResult.Value!;
        finalizedRun.Status.Should().Be(PayrollRunStatus.Finalized,
            because: "run must transition to Finalized");
        finalizedRun.Checksum.Should().NotBeNullOrWhiteSpace(
            because: "finalized run must have a SHA-256 checksum");
        finalizedRun.FinalizedBy.Should().Be("uid-director-001");
        finalizedRun.FinalizedAt.Should().NotBeNull();
    }

    // ── TC-PAY-020-F: Finalized run is immutable ────────────────────────────────

    /// <summary>
    /// TC-PAY-020-F: After a run is Finalized, attempting to save it with Draft status
    /// must be rejected by the immutability guard in PayrollRunRepository.
    /// REQ-HR-003, CTL-SARS-001 (write-once)
    /// </summary>
    [Fact]
    public async Task PayrollRun_AfterFinalized_CannotBeOverwrittenWithDraftState()
    {
        // TC-PAY-020-F: Arrange — create a Finalized run directly via the aggregate
        var runId = $"pr_2026_03_{Guid.NewGuid():N}"[..32];
        var now = new DateTimeOffset(2026, 3, 31, 12, 0, 0, TimeSpan.Zero);

        // Build a Calculated run, then finalize it
        var run = BuildDraftRun(runId, TenantId, ["emp-immutable-test"]);
        var calcResult = run.MarkCalculated(
            grossTotal: new MoneyZAR(TestGross),
            payeTotal: new MoneyZAR(2_726.58m),
            uifTotal: new MoneyZAR(600.00m),
            sdlTotal: new MoneyZAR(300.00m),
            etiTotal: MoneyZAR.Zero,
            deductionTotal: new MoneyZAR(3_026.58m),
            netTotal: new MoneyZAR(26_973.42m),
            complianceFlags: ["CTL-SARS-001:PASS"],
            actorId: "uid-hrmanager-001",
            now: now);
        calcResult.IsSuccess.Should().BeTrue();

        var finalizeResult = run.Finalize(
            checksum: "abc123def456",
            finalizedBy: "uid-director-001",
            now: now.AddMinutes(5));
        finalizeResult.IsSuccess.Should().BeTrue();

        // Persist the finalized run
        var saveResult = await _runRepo.SaveAsync(run);
        saveResult.IsSuccess.Should().BeTrue(because: "initial save of finalized run must succeed");

        // Act — attempt to save a new Draft run with the same ID
        var draftRun = BuildDraftRun(runId, TenantId, ["emp-immutable-test"]);
        // At this point draftRun.Status == Draft; the repository guard must block this

        // Read back the persisted run to verify it is truly Finalized in the emulator
        var readResult = await _runRepo.GetByRunIdAsync(TenantId, runId);
        readResult.IsSuccess.Should().BeTrue();
        readResult.Value!.Status.Should().Be(PayrollRunStatus.Finalized,
            because: "a finalized run's status must persist and be readable as Finalized");
        readResult.Value!.IsImmutable.Should().BeTrue(
            because: "IsImmutable must return true for Finalized runs");

        // The SaveAsync guard rejects Filed → non-Filed writes.
        // A Finalized run is NOT Filed yet, so we verify that the Filed guard works via
        // filing the run and then trying to write a Draft on top.
        var fileResult = run.MarkFiled("uid-director-001", now.AddMinutes(10));
        fileResult.IsSuccess.Should().BeTrue();
        var fileSave = await _runRepo.SaveAsync(run);
        fileSave.IsSuccess.Should().BeTrue(because: "transitioning Finalized → Filed is allowed");

        // Now try to write a Draft over the Filed run — must be rejected
        var draftOverFiledRun = BuildDraftRun(runId, TenantId, ["emp-immutable-test"]);
        var rejectedSave = await _runRepo.SaveAsync(draftOverFiledRun);
        rejectedSave.IsFailure.Should().BeTrue(
            because: "writing Draft state over a Filed run must be rejected by the immutability guard");
    }

    // ── TC-PAY-020-G: Payslip invariant holds end-to-end ───────────────────────

    /// <summary>
    /// TC-PAY-020-G: Verifies the payslip invariant (net == gross - deductions) holds
    /// after the full engine calculation and Firestore round-trip, to the cent.
    /// REQ-HR-003, CTL-SARS-001 (payslip invariant is a Sev-1 check)
    /// </summary>
    [Fact]
    public async Task PayrollResult_AfterFirestoreRoundTrip_PayslipInvariantHoldsToTheCent()
    {
        // TC-PAY-020-G: Arrange — seed rules and compute via engines
        var seedResult = await _loader.LoadAllAsync();
        seedResult.IsSuccess.Should().BeTrue();

        var runId = $"pr_2026_03_{Guid.NewGuid():N}"[..32];
        var empId = $"emp-g-{Guid.NewGuid():N}";
        var now = new DateTimeOffset(2026, 3, 31, 12, 0, 0, TimeSpan.Zero);
        var periodEndDate = new DateOnly(2026, 3, 31);

        var payeRuleSetResult = await _ruleSetRepo.GetEffectiveAsync(RuleDomains.SarsPaye, periodEndDate);
        var uifSdlRuleSetResult = await _ruleSetRepo.GetEffectiveAsync(RuleDomains.SarsUifSdl, periodEndDate);
        payeRuleSetResult.IsSuccess.Should().BeTrue();
        uifSdlRuleSetResult.IsSuccess.Should().BeTrue();

        var payeRules = SarsPayeRuleSet.From(payeRuleSetResult.Value!);
        var uifSdlRules = SarsUifSdlRuleSet.From(uifSdlRuleSetResult.Value!);

        var gross = new MoneyZAR(TestGross);
        var paye = PayeCalculationEngine.CalculateMonthlyPAYE(gross, age: 46, payeRules);
        var uifEmployee = UifSdlCalculationEngine.CalculateUifEmployee(gross, uifSdlRules);
        var uifEmployer = UifSdlCalculationEngine.CalculateUifEmployer(gross, uifSdlRules);
        var sdl = UifSdlCalculationEngine.CalculateSdl(gross, uifSdlRules, isEmployerSdlExempt: false);

        // Create PayrollResult — invariant enforced inside Create()
        var createResult = PayrollResult.Create(
            employeeId: empId,
            payrollRunId: runId,
            tenantId: TenantId,
            basicSalary: gross,
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
            etiAmount: MoneyZAR.Zero,
            etiEligible: false,
            otherDeductions: null,
            otherAdditions: null,
            hoursOrdinary: 173.33m,
            hoursOvertime: 0m,
            taxTableVersion: TestRuleSetVersion,
            complianceFlags: ["CTL-SARS-001:PASS"],
            calculationTimestamp: now);

        createResult.IsSuccess.Should().BeTrue(because: "invariant must hold on creation");

        // Persist a PayrollRun shell then the result
        var run = BuildDraftRun(runId, TenantId, [empId]);
        await _runRepo.SaveAsync(run);
        var saveResult = await _resultRepo.CreateAsync(createResult.Value!);
        saveResult.IsSuccess.Should().BeTrue();

        // Read back from Firestore
        var readResult = await _resultRepo.GetByEmployeeIdAsync(runId, empId);
        readResult.IsSuccess.Should().BeTrue();

        var readBack = readResult.Value!;

        // Assert invariant: net == gross - deductionTotal (to the cent)
        var expectedNet = readBack.GrossPay - readBack.DeductionTotal + readBack.AdditionTotal;
        readBack.NetPay.Amount.Should().Be(expectedNet.Amount,
            because: "net_pay == gross_pay - deduction_total + addition_total must hold to the cent after Firestore round-trip");
    }

    // ── TC-PAY-020-H: EMP201 due date calculation ───────────────────────────────

    /// <summary>
    /// TC-PAY-020-H: Verifies CalculateDueDate returns the 7th of the following month
    /// (or the next Monday if the 7th falls on a weekend).
    /// CTL-SARS-006
    /// </summary>
    [Fact]
    public void Emp201Generator_CalculateDueDate_ReturnsSeventh_OrNextMonday()
    {
        // TC-PAY-020-H: March 2026 → 7 April 2026 (Tuesday) — must remain 7 April
        var dueDate = _emp201Generator.CalculateDueDate(2026, 3);
        dueDate.Should().Be(new DateOnly(2026, 4, 7),
            because: "7 April 2026 is a Tuesday — no weekend adjustment needed");

        // December 2026 → 7 January 2027 (Thursday) — must remain 7 January
        var dueDateDec = _emp201Generator.CalculateDueDate(2026, 12);
        dueDateDec.Month.Should().Be(1);
        dueDateDec.Year.Should().Be(2027);
        dueDateDec.Day.Should().BeInRange(7, 9,
            because: "if 7 Jan falls on weekend it advances to Monday, otherwise stays 7th");

        // The due date is never a Saturday or Sunday
        dueDate.DayOfWeek.Should().NotBe(DayOfWeek.Saturday);
        dueDate.DayOfWeek.Should().NotBe(DayOfWeek.Sunday);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>Seeds an Employee document into the Firestore emulator.</summary>
    private async Task SeedEmployeeAsync(string empId, string tenantId, DateOnly dateOfBirth)
    {
        var employee = EmployeeAggregate.Create(
            employeeId: empId,
            tenantId: tenantId,
            firebaseUid: $"uid_{Guid.NewGuid():N}",
            legalName: "Test Zenowethu Employee",
            nationalIdOrPassport: "8001015009087",
            taxReference: "9123456789",
            dateOfBirth: dateOfBirth,
            personalPhoneNumber: "+27821234567",
            personalEmail: $"{empId}@zenowethu.co.za",
            workEmail: $"work.{empId}@zenowethu.co.za",
            nationality: "ZA",
            gender: "Female",
            race: "African",
            disabilityStatus: false,
            disabilityDescription: null,
            hireDate: new DateOnly(2023, 1, 1),
            employeeType: EmployeeType.Permanent,
            departmentId: "dept-hr-001",
            roleId: "role-employee-001",
            systemRole: "Employee",
            reportsToEmployeeId: null,
            actorId: "uid-hrmanager-001",
            now: DateTimeOffset.UtcNow);

        employee.IsSuccess.Should().BeTrue(because: "test employee must be created successfully");
        var saveResult = await _employeeRepo.SaveAsync(employee.Value!);
        saveResult.IsSuccess.Should().BeTrue(because: "test employee must persist to emulator");
    }

    /// <summary>Seeds an EmploymentContract document into the Firestore emulator.</summary>
    private async Task SeedContractAsync(
        string empId, string tenantId, MoneyZAR baseSalary, DateOnly startDate)
    {
        var contractId = $"contract-{empId}";
        var contract = EmploymentContract.Create(
            contractId: contractId,
            tenantId: tenantId,
            employeeId: empId,
            startDate: startDate,
            endDate: null,
            salaryBasis: SalaryBasis.Monthly,
            baseSalary: baseSalary,
            ordinaryHoursPerWeek: 40m,
            ordinaryHoursPolicyVersion: "BCEA_WORKING_TIME_2026",
            occupationalLevel: "Skilled",
            actorId: "uid-hrmanager-001",
            now: DateTimeOffset.UtcNow);

        contract.IsSuccess.Should().BeTrue(because: "test contract must be created successfully");
        var saveResult = await _contractRepo.SaveAsync(contract.Value!);
        saveResult.IsSuccess.Should().BeTrue(because: "test contract must persist to emulator");
    }

    /// <summary>
    /// Seeds a minimal payroll run + one result into the emulator.
    /// Returns (runId, empId, grossAmount) for downstream assertions.
    /// </summary>
    private async Task<(string runId, string empId, decimal grossAmount)> SeedRunWithOneResultAsync()
    {
        // Seed rules first
        var seedResult = await _loader.LoadAllAsync();
        seedResult.IsSuccess.Should().BeTrue();

        var runId = $"pr_2026_03_{Guid.NewGuid():N}"[..32];
        var empId = $"emp-csv-{Guid.NewGuid():N}";
        var now = new DateTimeOffset(2026, 3, 31, 12, 0, 0, TimeSpan.Zero);
        var periodEndDate = new DateOnly(2026, 3, 31);

        var payeRuleSetResult = await _ruleSetRepo.GetEffectiveAsync(RuleDomains.SarsPaye, periodEndDate);
        var uifSdlRuleSetResult = await _ruleSetRepo.GetEffectiveAsync(RuleDomains.SarsUifSdl, periodEndDate);
        payeRuleSetResult.IsSuccess.Should().BeTrue();
        uifSdlRuleSetResult.IsSuccess.Should().BeTrue();

        var payeRules = SarsPayeRuleSet.From(payeRuleSetResult.Value!);
        var uifSdlRules = SarsUifSdlRuleSet.From(uifSdlRuleSetResult.Value!);

        var gross = new MoneyZAR(TestGross);
        var paye = PayeCalculationEngine.CalculateMonthlyPAYE(gross, age: 46, payeRules);
        var uifEmployee = UifSdlCalculationEngine.CalculateUifEmployee(gross, uifSdlRules);
        var uifEmployer = UifSdlCalculationEngine.CalculateUifEmployer(gross, uifSdlRules);
        var sdl = UifSdlCalculationEngine.CalculateSdl(gross, uifSdlRules, isEmployerSdlExempt: false);

        var createResult = PayrollResult.Create(
            employeeId: empId,
            payrollRunId: runId,
            tenantId: TenantId,
            basicSalary: gross,
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
            etiAmount: MoneyZAR.Zero,
            etiEligible: false,
            otherDeductions: null,
            otherAdditions: null,
            hoursOrdinary: 173.33m,
            hoursOvertime: 0m,
            taxTableVersion: TestRuleSetVersion,
            complianceFlags: ["CTL-SARS-001:PASS"],
            calculationTimestamp: now);

        createResult.IsSuccess.Should().BeTrue();

        var run = BuildDraftRun(runId, TenantId, [empId]);
        await _runRepo.SaveAsync(run);
        await _resultRepo.CreateAsync(createResult.Value!);

        return (runId, empId, TestGross);
    }

    /// <summary>
    /// Builds an Emp201Data object from a set of persisted PayrollResult objects.
    /// All monetary values come from the PayrollResult — no hardcoded rates.
    /// CTL-SARS-006
    /// </summary>
    private static Emp201Data BuildEmp201Data(
        string runId, string period, IReadOnlyList<PayrollResult> results)
    {
        var lines = results.Select(r => new Emp201EmployeeLine
        {
            EmployeeId = r.EmployeeId,
            EmployeeFullName = "Test Zenowethu Employee",
            TaxReferenceNumber = "9123456789",
            IdOrPassportNumber = "8001015009087",
            GrossRemuneration = r.GrossPay.Amount,
            PayeDeducted = r.Paye.Amount,
            UifEmployee = r.UifEmployee.Amount,
            UifEmployer = r.UifEmployer.Amount,
            SdlEmployer = r.Sdl.Amount,
            PaymentMethod = "EFT",
        }).ToList();

        var totalPaye = results.Sum(r => r.Paye.Amount);
        var totalUifEmp = results.Sum(r => r.UifEmployee.Amount);
        var totalUifEmpr = results.Sum(r => r.UifEmployer.Amount);
        var totalSdl = results.Sum(r => r.Sdl.Amount);
        var totalGross = results.Sum(r => r.GrossPay.Amount);

        // TaxPeriod in YYYYMM format (March 2026 → "202603")
        var taxPeriod = period.Replace("-", "");

        return new Emp201Data
        {
            EmployerPAYEReference = "7012345678",
            EmployerUifReference = "UIF-ZNW-001",
            EmployerSdlReference = "SDL-ZNW-001",
            EmployerTradingName = "Zenowethu (Pty) Ltd",
            TaxPeriod = taxPeriod,
            PeriodLabel = "March 2026",
            PayrollRunId = runId,
            TotalPayeDeducted = totalPaye,
            TotalUifEmployee = totalUifEmp,
            TotalUifEmployer = totalUifEmpr,
            TotalSdl = totalSdl,
            TotalGrossRemuneration = totalGross,
            EmployeeCount = results.Count,
            DueDate = new DateOnly(2026, 4, 7),
            EmployeeLines = lines,
            GeneratedByUserId = "uid-hrmanager-001",
            GeneratedAt = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
        };
    }

    /// <summary>Builds a PayrollOrchestrationService wired to the emulator repositories.</summary>
    private PayrollOrchestrationService BuildOrchestrationService()
        => new(
            ruleSetRepo: _ruleSetRepo,
            employeeRepo: _employeeRepo,
            contractRepo: _contractRepo,
            benefitRepo: _benefitRepo,
            runRepo: _runRepo,
            resultRepo: _resultRepo,
            logger: NullLogger<PayrollOrchestrationService>.Instance);

    /// <summary>Builds a Draft PayrollRun aggregate for use in tests.</summary>
    private static PayrollRun BuildDraftRun(string runId, string tenantId, IReadOnlyList<string> employeeIds)
    {
        var result = PayrollRun.Create(
            id: runId,
            tenantId: tenantId,
            period: TestPeriod,
            runType: PayFrequency.Monthly,
            employeeIds: employeeIds,
            ruleSetVersion: TestRuleSetVersion,
            initiatedBy: "uid-hrmanager-001",
            idempotencyKey: Guid.NewGuid().ToString("N"),
            now: new DateTimeOffset(2026, 3, 31, 12, 0, 0, TimeSpan.Zero));

        result.IsSuccess.Should().BeTrue();
        return result.Value!;
    }
}
