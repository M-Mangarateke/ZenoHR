// REQ-OPS-001: UAT — Pilot tenant payroll workflow integration tests.
// TC-UAT-PAY-001 through TC-UAT-PAY-007: End-to-end payroll scenarios for Zenowethu (Pty) Ltd.
// TASK-156: Simulate real tenant payroll workflows using actual calculation engines.
// All monetary values use MoneyZAR (decimal). No float/double. CTL-SARS-001.

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ZenoHR.Domain.Common;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Infrastructure.Seeding;
using ZenoHR.Infrastructure.Services;
using ZenoHR.Integration.Tests.Infrastructure;
using ZenoHR.Module.Employee.Aggregates;
using EmployeeAggregate = ZenoHR.Module.Employee.Aggregates.Employee;
using ZenoHR.Module.Payroll.Calculation;
using ZenoHR.Module.Payroll.Entities;

namespace ZenoHR.Integration.Tests.Uat;

/// <summary>
/// UAT payroll workflow tests for the Zenowethu pilot tenant.
/// These tests exercise the real calculation engines (PAYE, UIF, SDL, ETI)
/// against multiple salary levels to validate correctness end-to-end.
/// REQ-OPS-001, REQ-HR-003, CTL-SARS-001
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class UatPayrollWorkflowTests : IntegrationTestBase
{
    // ── Pilot tenant constants ──────────────────────────────────────────────
    // REQ-OPS-001: Zenowethu pilot tenant identification.

    private const string PilotTenantId = "zenowethu-001";
    private const string TestPeriod = "2026-03";
    private const string TestRuleSetVersion = "SARS_PAYE_2026";

    // ── Repositories ────────────────────────────────────────────────────────

    private readonly PayrollRunRepository _runRepo;
    private readonly PayrollResultRepository _resultRepo;
    private readonly StatutoryRuleSetRepository _ruleSetRepo;
    private readonly EmployeeRepository _employeeRepo;
    private readonly EmploymentContractRepository _contractRepo;
    private readonly EmployeeBenefitRepository _benefitRepo;
    private readonly StatutoryRuleSetLoader _loader;

    public UatPayrollWorkflowTests(FirestoreEmulatorFixture fixture) : base(fixture)
    {
        _runRepo = new PayrollRunRepository(fixture.Db, NullLogger<PayrollRunRepository>.Instance);
        _resultRepo = new PayrollResultRepository(fixture.Db);
        _ruleSetRepo = new StatutoryRuleSetRepository(fixture.Db, NullLogger<StatutoryRuleSetRepository>.Instance);
        _employeeRepo = new EmployeeRepository(fixture.Db, NullLogger<EmployeeRepository>.Instance);
        _contractRepo = new EmploymentContractRepository(fixture.Db, NullLogger<EmploymentContractRepository>.Instance);
        _benefitRepo = new EmployeeBenefitRepository(fixture.Db);
        _loader = new StatutoryRuleSetLoader(fixture.Db);
    }

    // ── TC-UAT-PAY-001: Monthly payroll for 5 employees at different salary levels ──

    /// <summary>
    /// TC-UAT-PAY-001: Runs monthly payroll for 5 Zenowethu employees at salary levels
    /// R8,000 / R15,000 / R25,000 / R45,000 / R85,000 and verifies PAYE, UIF, SDL,
    /// and the payslip invariant for each.
    /// REQ-OPS-001, REQ-HR-003, CTL-SARS-001
    /// </summary>
    [Fact]
    public async Task MonthlyPayroll_FiveEmployeesAtDifferentSalaryLevels_AllCalculationsCorrect()
    {
        // TC-UAT-PAY-001: Arrange — seed statutory rules
        var seedResult = await _loader.LoadAllAsync();
        seedResult.IsSuccess.Should().BeTrue(because: "statutory seed data must load for UAT payroll");

        var periodEndDate = new DateOnly(2026, 3, 31);
        var now = new DateTimeOffset(2026, 3, 31, 12, 0, 0, TimeSpan.Zero);

        var payeRules = SarsPayeRuleSet.From(
            (await _ruleSetRepo.GetEffectiveAsync(RuleDomains.SarsPaye, periodEndDate)).Value!);
        var uifSdlRules = SarsUifSdlRuleSet.From(
            (await _ruleSetRepo.GetEffectiveAsync(RuleDomains.SarsUifSdl, periodEndDate)).Value!);

        // Five pilot employees at different salary levels
        var salaryLevels = new[]
        {
            ("emp-uat-001", 8_000.00m, "Lindiwe Dlamini"),
            ("emp-uat-002", 15_000.00m, "Thabo Molefe"),
            ("emp-uat-003", 25_000.00m, "Ayanda Nkosi"),
            ("emp-uat-004", 45_000.00m, "Sipho Zulu"),
            ("emp-uat-005", 85_000.00m, "Naledi Khumalo"),
        };

        foreach (var (empId, salary, _) in salaryLevels)
        {
            await SeedEmployeeAsync(empId, PilotTenantId, new DateOnly(1990, 5, 15));
            await SeedContractAsync(empId, PilotTenantId, new MoneyZAR(salary), new DateOnly(2023, 1, 1));
        }

        var orchestrator = BuildOrchestrationService();
        var employeeIds = salaryLevels.Select(s => s.Item1).ToList();

        // Act — run monthly payroll for all 5 employees
        var result = await orchestrator.RunPayrollAsync(
            tenantId: PilotTenantId,
            period: TestPeriod,
            runType: PayFrequency.Monthly,
            employeeIds: employeeIds,
            ruleSetVersion: TestRuleSetVersion,
            initiatedBy: "uid-hrmanager-001",
            idempotencyKey: Guid.NewGuid().ToString("N"),
            isSdlExempt: false,
            now: now);

        // Assert — run succeeded
        result.IsSuccess.Should().BeTrue(because:
            $"payroll run must succeed for 5 valid employees. Error: {(result.IsSuccess ? "" : result.Error.Message)}");

        var run = result.Value!;
        run.EmployeeCount.Should().Be(5);
        run.TenantId.Should().Be(PilotTenantId);

        // Assert — verify each employee's calculation
        foreach (var (empId, salary, _) in salaryLevels)
        {
            var empResult = await _resultRepo.GetByEmployeeIdAsync(run.Id, empId);
            empResult.IsSuccess.Should().BeTrue(because: $"result for {empId} must exist");

            var pr = empResult.Value!;
            var gross = new MoneyZAR(salary);

            // Payslip invariant: net == gross - deductions + additions
            var expectedNet = pr.GrossPay - pr.DeductionTotal + pr.AdditionTotal;
            pr.NetPay.Amount.Should().Be(expectedNet.Amount,
                because: $"payslip invariant must hold for {empId} (salary R{salary:N2})");

            // PAYE must be non-negative
            pr.Paye.Amount.Should().BeGreaterThanOrEqualTo(0m,
                because: "PAYE is floored at zero after rebates");

            // Verify PAYE matches engine calculation
            var expectedPaye = PayeCalculationEngine.CalculateMonthlyPAYE(gross, age: 36, payeRules);
            pr.Paye.Amount.Should().Be(expectedPaye.Amount,
                because: $"PAYE for {empId} must match engine calculation");

            // UIF employee: min(gross × 1%, R177.12)
            var expectedUif = UifSdlCalculationEngine.CalculateUifEmployee(gross, uifSdlRules);
            pr.UifEmployee.Amount.Should().Be(expectedUif.Amount,
                because: $"UIF for {empId} must match engine calculation");

            // SDL: employer-only, 1% of gross
            var expectedSdl = UifSdlCalculationEngine.CalculateSdl(gross, uifSdlRules, isEmployerSdlExempt: false);
            pr.Sdl.Amount.Should().Be(expectedSdl.Amount,
                because: $"SDL for {empId} must match engine calculation");
        }
    }

    // ── TC-UAT-PAY-002: UIF capped at R177.12 for high earners ─────────────

    /// <summary>
    /// TC-UAT-PAY-002: Verifies that UIF employee contribution is capped at R177.12
    /// for employees earning above the R17,712/month ceiling.
    /// REQ-OPS-001, REQ-HR-003, CTL-SARS-002
    /// </summary>
    [Theory]
    [InlineData(8_000.00)]   // Below ceiling: UIF = R80.00
    [InlineData(17_712.00)]  // At ceiling: UIF = R177.12
    [InlineData(25_000.00)]  // Above ceiling: UIF = R177.12
    [InlineData(45_000.00)]  // Well above ceiling: UIF = R177.12
    [InlineData(85_000.00)]  // High earner: UIF = R177.12
    public void UifEmployee_AtVariousSalaryLevels_CappedCorrectly(double salaryDouble)
    {
        // TC-UAT-PAY-002: Use real engine with test-created rule set
        var salary = (decimal)salaryDouble;
        var gross = new MoneyZAR(salary);
        var rules = SarsUifSdlRuleSet.CreateForTesting();

        // Act
        var uif = UifSdlCalculationEngine.CalculateUifEmployee(gross, rules);

        // Assert
        if (salary <= 17_712.00m)
        {
            // Below or at ceiling: UIF = gross × 1%
            var expected = (gross * 0.01m).RoundToCent();
            uif.Amount.Should().Be(expected.Amount,
                because: $"UIF at R{salary:N2} should be 1% of gross");
        }
        else
        {
            // Above ceiling: capped at R177.12
            uif.Amount.Should().Be(177.12m,
                because: $"UIF at R{salary:N2} must be capped at R177.12");
        }
    }

    // ── TC-UAT-PAY-003: SDL at 1% employer-only ────────────────────────────

    /// <summary>
    /// TC-UAT-PAY-003: Verifies SDL is exactly 1% of gross, employer-only, and
    /// exempt when the employer is SDL-exempt (annual payroll below R500k).
    /// REQ-OPS-001, REQ-HR-003, CTL-SARS-002
    /// </summary>
    [Theory]
    [InlineData(8_000.00, false, 80.00)]
    [InlineData(25_000.00, false, 250.00)]
    [InlineData(85_000.00, false, 850.00)]
    [InlineData(25_000.00, true, 0.00)]  // SDL-exempt employer
    public void Sdl_AtVariousSalaryLevels_CalculatedCorrectly(
        double salaryDouble, bool isSdlExempt, double expectedSdlDouble)
    {
        // TC-UAT-PAY-003: Pure engine test
        var salary = (decimal)salaryDouble;
        var expectedSdl = (decimal)expectedSdlDouble;
        var gross = new MoneyZAR(salary);
        var rules = SarsUifSdlRuleSet.CreateForTesting();

        // Act
        var sdl = UifSdlCalculationEngine.CalculateSdl(gross, rules, isEmployerSdlExempt: isSdlExempt);

        // Assert
        sdl.Amount.Should().Be(expectedSdl,
            because: $"SDL at R{salary:N2} (exempt={isSdlExempt}) should be R{expectedSdl:N2}");
    }

    // ── TC-UAT-PAY-004: Weekly payroll calculation ──────────────────────────

    /// <summary>
    /// TC-UAT-PAY-004: Verifies weekly PAYE uses ×52/÷52 annualization method.
    /// REQ-OPS-001, REQ-HR-003, CTL-SARS-001
    /// </summary>
    [Fact]
    public async Task WeeklyPaye_ForPilotEmployee_UsesCorrectAnnualizationFactor()
    {
        // TC-UAT-PAY-004: Arrange — seed rules
        var seedResult = await _loader.LoadAllAsync();
        seedResult.IsSuccess.Should().BeTrue();

        var periodEndDate = new DateOnly(2026, 3, 31);
        var payeRules = SarsPayeRuleSet.From(
            (await _ruleSetRepo.GetEffectiveAsync(RuleDomains.SarsPaye, periodEndDate)).Value!);

        // R5,000/week = R260,000/year (R5,000 × 52)
        var weeklyGross = new MoneyZAR(5_000.00m);
        const int age = 30;

        // Act
        var weeklyPaye = PayeCalculationEngine.CalculateWeeklyPAYE(weeklyGross, age, payeRules);

        // Assert — weekly PAYE must be positive and reasonable
        weeklyPaye.Amount.Should().BeGreaterThan(0m,
            because: "R5,000/week (R260k/year) exceeds the tax threshold");

        // Cross-check: annual tax from weekly annualization should approximate monthly
        // R260,000/12 ≈ R21,666.67/month
        var monthlyEquivalent = new MoneyZAR(260_000.00m / 12m);
        var monthlyPaye = PayeCalculationEngine.CalculateMonthlyPAYE(monthlyEquivalent, age, payeRules);

        // Weekly PAYE × 52 should approximate monthly PAYE × 12 (within 1% tolerance due to rounding)
        var annualFromWeekly = weeklyPaye.Amount * 52m;
        var annualFromMonthly = monthlyPaye.Amount * 12m;
        var tolerance = annualFromMonthly * 0.01m;

        annualFromWeekly.Should().BeApproximately(annualFromMonthly, tolerance,
            because: "weekly and monthly annualization should produce similar annual PAYE totals");
    }

    // ── TC-UAT-PAY-005: ETI calculation for qualifying employee ─────────────

    /// <summary>
    /// TC-UAT-PAY-005: Verifies ETI calculation for a Zenowethu employee who qualifies:
    /// age 23 (18–29 range), salary R5,000/month (R2,500–R7,500 range), employed 6 months (Tier 1).
    /// REQ-OPS-001, REQ-HR-003, CTL-SARS-003
    /// </summary>
    [Fact]
    public async Task Eti_QualifyingEmployee_CalculatesCorrectIncentive()
    {
        // TC-UAT-PAY-005: Arrange — seed ETI rules
        var seedResult = await _loader.LoadAllAsync();
        seedResult.IsSuccess.Should().BeTrue();

        var periodEndDate = new DateOnly(2026, 3, 31);
        var etiRuleSetResult = await _ruleSetRepo.GetEffectiveAsync(RuleDomains.SarsEti, periodEndDate);
        etiRuleSetResult.IsSuccess.Should().BeTrue(because: "ETI rule set must be present after seeding");
        var etiRules = SarsEtiRuleSet.From(etiRuleSetResult.Value!);

        // Qualifying employee: age 23, R5,000/month, employed since 2025-10-01 (6 months)
        var employmentStartDate = new DateOnly(2025, 10, 1);
        var calculationDate = new DateOnly(2026, 3, 31);
        const int age = 23;
        var monthlyRemuneration = new MoneyZAR(5_000.00m);
        const int standardHours = 160;

        // Act — determine tier and calculate
        var tier = EtiCalculationEngine.GetTier(employmentStartDate, calculationDate);
        var isEligible = EtiCalculationEngine.IsEligible(age, monthlyRemuneration, tier, etiRules);
        var etiAmount = EtiCalculationEngine.CalculateMonthlyEti(monthlyRemuneration, tier, standardHours, etiRules);

        // Assert
        tier.Should().Be(EtiTier.Tier1,
            because: "6 months of employment falls within the first 12-month tier");
        isEligible.Should().BeTrue(
            because: "age 23 and R5,000/month both fall within ETI eligibility range");
        etiAmount.Amount.Should().BeGreaterThan(0m,
            because: "a qualifying Tier 1 employee at R5,000/month must receive ETI");
        etiAmount.Amount.Should().BeLessThanOrEqualTo(1_500.00m,
            because: "Tier 1 ETI maximum is R1,500/month per SARS guidelines");
    }

    // ── TC-UAT-PAY-006: ETI ineligible — age outside range ──────────────────

    /// <summary>
    /// TC-UAT-PAY-006: Verifies ETI returns zero for an employee aged 35 (outside 18–29 range).
    /// REQ-OPS-001, REQ-HR-003, CTL-SARS-003
    /// </summary>
    [Fact]
    public async Task Eti_EmployeeAge35_ReturnsZero()
    {
        // TC-UAT-PAY-006: Arrange
        var seedResult = await _loader.LoadAllAsync();
        seedResult.IsSuccess.Should().BeTrue();

        var periodEndDate = new DateOnly(2026, 3, 31);
        var etiRules = SarsEtiRuleSet.From(
            (await _ruleSetRepo.GetEffectiveAsync(RuleDomains.SarsEti, periodEndDate)).Value!);

        var monthlyRemuneration = new MoneyZAR(5_000.00m);
        var tier = EtiTier.Tier1; // Even if tier qualifies, age disqualifies

        // Act
        var isEligible = EtiCalculationEngine.IsEligible(
            ageAtCalculationDate: 35, monthlyRemuneration, tier, etiRules);

        // Assert
        isEligible.Should().BeFalse(
            because: "employee aged 35 is outside the 18–29 ETI eligibility range");
    }

    // ── TC-UAT-PAY-007: Payslip invariant holds for all salary levels ───────

    /// <summary>
    /// TC-UAT-PAY-007: Creates PayrollResult for each of the 5 salary levels and verifies
    /// the payslip invariant (net == gross - deductions) holds to the cent.
    /// REQ-OPS-001, REQ-HR-003, CTL-SARS-001
    /// </summary>
    [Theory]
    [InlineData(8_000.00)]
    [InlineData(15_000.00)]
    [InlineData(25_000.00)]
    [InlineData(45_000.00)]
    [InlineData(85_000.00)]
    public void PayslipInvariant_AtEachSalaryLevel_HoldsToTheCent(double salaryDouble)
    {
        // TC-UAT-PAY-007: Use real engines with test-created rule sets
        var salary = (decimal)salaryDouble;
        var gross = new MoneyZAR(salary);
        var payeRules = SarsPayeRuleSet.CreateForTesting(
            brackets:
            [
                new PayeTaxBracket { Min = 1m, Max = 237_100m, Rate = 0.18m, BaseTax = 0m },
                new PayeTaxBracket { Min = 237_101m, Max = 370_500m, Rate = 0.26m, BaseTax = 42_678m },
                new PayeTaxBracket { Min = 370_501m, Max = 512_800m, Rate = 0.31m, BaseTax = 77_362m },
                new PayeTaxBracket { Min = 512_801m, Max = 673_000m, Rate = 0.36m, BaseTax = 121_475m },
                new PayeTaxBracket { Min = 673_001m, Max = 857_900m, Rate = 0.39m, BaseTax = 179_147m },
                new PayeTaxBracket { Min = 857_901m, Max = 1_817_000m, Rate = 0.41m, BaseTax = 251_258m },
                new PayeTaxBracket { Min = 1_817_001m, Max = null, Rate = 0.45m, BaseTax = 644_489m },
            ],
            primary: 17_235m,
            secondary: 9_444m,
            tertiary: 3_145m,
            thresholdBelow65: 95_750m,
            thresholdAge65To74: 148_217m,
            thresholdAge75Plus: 165_689m);

        var uifSdlRules = SarsUifSdlRuleSet.CreateForTesting();

        var paye = PayeCalculationEngine.CalculateMonthlyPAYE(gross, age: 36, payeRules);
        var uifEmp = UifSdlCalculationEngine.CalculateUifEmployee(gross, uifSdlRules);
        var uifEr = UifSdlCalculationEngine.CalculateUifEmployer(gross, uifSdlRules);
        var sdl = UifSdlCalculationEngine.CalculateSdl(gross, uifSdlRules, isEmployerSdlExempt: false);

        // Act — create PayrollResult (invariant verified inside Create)
        var runId = $"pr_uat_{Guid.NewGuid():N}"[..32];
        var createResult = PayrollResult.Create(
            employeeId: $"emp-inv-{salary}",
            payrollRunId: runId,
            tenantId: PilotTenantId,
            basicSalary: gross,
            overtimePay: MoneyZAR.Zero,
            allowances: MoneyZAR.Zero,
            paye: paye,
            uifEmployee: uifEmp,
            uifEmployer: uifEr,
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
            calculationTimestamp: DateTimeOffset.UtcNow);

        // Assert — invariant enforced by Create (would fail if violated)
        createResult.IsSuccess.Should().BeTrue(because:
            $"PayrollResult.Create must succeed with valid inputs at R{salary:N2}. Error: {(createResult.IsSuccess ? "" : createResult.Error.Message)}");

        var pr = createResult.Value!;
        var expectedNet = pr.GrossPay - pr.DeductionTotal + pr.AdditionTotal;
        pr.NetPay.Amount.Should().Be(expectedNet.Amount,
            because: $"payslip invariant must hold to the cent at R{salary:N2}");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Seeds an Employee document into the Firestore emulator. REQ-OPS-001.</summary>
    private async Task SeedEmployeeAsync(string empId, string tenantId, DateOnly dateOfBirth)
    {
        var employee = EmployeeAggregate.Create(
            employeeId: empId,
            tenantId: tenantId,
            firebaseUid: $"uid_{Guid.NewGuid():N}",
            legalName: $"UAT Employee {empId}",
            nationalIdOrPassport: "9005150001087",
            taxReference: "0123456789",
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

        employee.IsSuccess.Should().BeTrue(because: "UAT employee must be created successfully");
        var saveResult = await _employeeRepo.SaveAsync(employee.Value!);
        saveResult.IsSuccess.Should().BeTrue(because: "UAT employee must persist to emulator");
    }

    /// <summary>Seeds an EmploymentContract into the Firestore emulator. REQ-OPS-001.</summary>
    private async Task SeedContractAsync(
        string empId, string tenantId, MoneyZAR baseSalary, DateOnly startDate)
    {
        var contract = EmploymentContract.Create(
            contractId: $"contract-{empId}",
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

        contract.IsSuccess.Should().BeTrue(because: "UAT contract must be created successfully");
        var saveResult = await _contractRepo.SaveAsync(contract.Value!);
        saveResult.IsSuccess.Should().BeTrue(because: "UAT contract must persist to emulator");
    }

    /// <summary>Builds a PayrollOrchestrationService wired to emulator repositories. REQ-OPS-001.</summary>
    private PayrollOrchestrationService BuildOrchestrationService()
        => new(
            ruleSetRepo: _ruleSetRepo,
            employeeRepo: _employeeRepo,
            contractRepo: _contractRepo,
            benefitRepo: _benefitRepo,
            runRepo: _runRepo,
            resultRepo: _resultRepo,
            logger: NullLogger<PayrollOrchestrationService>.Instance);
}
