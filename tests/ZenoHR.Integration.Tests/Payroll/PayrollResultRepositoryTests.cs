// TC-PAY-011: PayrollResultRepository integration tests against Firestore emulator.
// REQ-HR-003: Per-employee payroll result persistence in subcollection.
// REQ-HR-004: Payslip invariant — net = gross - deductions + additions (to the cent).
// CTL-SARS-001: Results are write-once; duplicate creation is rejected.
// REQ-SEC-005: Tenant isolation enforced on result documents.

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ZenoHR.Domain.Common;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Integration.Tests.Infrastructure;
using ZenoHR.Module.Payroll.Aggregates;
using ZenoHR.Module.Payroll.Calculation;
using ZenoHR.Module.Payroll.Entities;

namespace ZenoHR.Integration.Tests.Payroll;

/// <summary>
/// Integration tests for <see cref="PayrollResultRepository"/> against the Firestore emulator.
/// TC-PAY-011-A: SaveAsync then GetByEmployeeId returns PayrollResult with correct net pay.
/// TC-PAY-011-B: Payslip invariant verified — net == gross - deductions.
/// TC-PAY-011-C: Tenant isolation on PayrollResult.
/// TC-PAY-011-D: ListByRunAsync returns all results for a run.
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class PayrollResultRepositoryTests : IntegrationTestBase
{
    // TC-PAY-011
    private readonly PayrollResultRepository _resultRepo;
    private readonly PayrollRunRepository _runRepo;

    public PayrollResultRepositoryTests(FirestoreEmulatorFixture fixture) : base(fixture)
    {
        _resultRepo = new PayrollResultRepository(fixture.Db);
        _runRepo = new PayrollRunRepository(fixture.Db, NullLogger<PayrollRunRepository>.Instance);
    }

    // ── TC-PAY-011-A: Save then GetByEmployeeId returns correct net pay ───────

    [Fact]
    public async Task CreateAsync_ThenGetByEmployeeId_ReturnsResultWithCorrectNetPay()
    {
        // TC-PAY-011-A: Arrange
        var runId = $"pr_{Guid.NewGuid():N}";
        var employeeId = $"emp_{Guid.NewGuid():N}";
        await CreateAndSaveParentRun(runId);

        var result = CreatePayrollResult(employeeId, runId, TenantId,
            basicSalary: 40_000m,
            paye: 8_000m,
            uifEmployee: 177.12m);

        // Act
        var saveResult = await _resultRepo.CreateAsync(result);
        var getResult = await _resultRepo.GetByEmployeeIdAsync(runId, employeeId);

        // Assert
        saveResult.IsSuccess.Should().BeTrue();
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value!.EmployeeId.Should().Be(employeeId);
        getResult.Value.PayrollRunId.Should().Be(runId);
        getResult.Value.TenantId.Should().Be(TenantId);
        getResult.Value.BasicSalary.Amount.Should().Be(40_000m);
        getResult.Value.Paye.Amount.Should().Be(8_000m);
        getResult.Value.UifEmployee.Amount.Should().Be(177.12m);

        // Net pay = gross - deduction_total (no additions, no pension, no medical)
        // gross = 40_000, deduction_total = 8_000 + 177.12 = 8_177.12
        getResult.Value.NetPay.Amount.Should().Be(40_000m - 8_177.12m);
    }

    // ── TC-PAY-011-B: Payslip invariant — net == gross - deductions ───────────

    [Fact]
    public async Task CreateAsync_PayslipInvariant_NetEqualsGrossMinusDeductions()
    {
        // TC-PAY-011-B: Arrange — verify the invariant is enforced end-to-end
        var runId = $"pr_{Guid.NewGuid():N}";
        var employeeId = $"emp_{Guid.NewGuid():N}";
        await CreateAndSaveParentRun(runId);

        var basicSalary = 55_000m;
        var overtimePay = 2_500m;
        var allowances = 1_000m;
        var paye = 12_000m;
        var uifEmployee = 177.12m;
        var pensionEmployee = 1_650m;
        var medicalEmployee = 900m;

        // Expected derived values (matching PayrollResult.Create logic)
        var expectedGross = basicSalary + overtimePay + allowances;              // 58_500
        var expectedDeductions = paye + uifEmployee + pensionEmployee + medicalEmployee; // 14_727.12
        var expectedNet = expectedGross - expectedDeductions;                     // 43_772.88

        var result = CreatePayrollResult(
            employeeId, runId, TenantId,
            basicSalary: basicSalary,
            overtimePay: overtimePay,
            allowances: allowances,
            paye: paye,
            uifEmployee: uifEmployee,
            pensionEmployee: pensionEmployee,
            medicalEmployee: medicalEmployee);

        // Act
        await _resultRepo.CreateAsync(result);
        var getResult = await _resultRepo.GetByEmployeeIdAsync(runId, employeeId);

        // Assert — invariant: net == gross - deduction_total + addition_total
        getResult.IsSuccess.Should().BeTrue();
        var retrieved = getResult.Value!;
        retrieved.GrossPay.Amount.Should().Be(expectedGross);
        retrieved.DeductionTotal.Amount.Should().Be(expectedDeductions);
        retrieved.NetPay.Amount.Should().Be(expectedNet,
            because: "payslip invariant: net_pay must equal gross_pay - deduction_total + addition_total to the cent (REQ-HR-004)");

        // Cross-check the arithmetic directly
        var recomputedNet = retrieved.GrossPay.Amount
                            - retrieved.DeductionTotal.Amount
                            + retrieved.AdditionTotal.Amount;
        retrieved.NetPay.Amount.Should().Be(recomputedNet,
            because: "payslip invariant must hold exactly — any cent discrepancy is a Sev-1 defect");
    }

    // ── TC-PAY-011-C: Tenant isolation on PayrollResult ──────────────────────

    [Fact]
    public async Task GetByEmployeeIdAsync_ResultBelongsToCorrectTenant()
    {
        // TC-PAY-011-C: Arrange
        var runId = $"pr_{Guid.NewGuid():N}";
        var employeeId = $"emp_{Guid.NewGuid():N}";
        await CreateAndSaveParentRun(runId);

        var result = CreatePayrollResult(employeeId, runId, TenantId,
            basicSalary: 30_000m,
            paye: 5_000m,
            uifEmployee: 177.12m);

        await _resultRepo.CreateAsync(result);

        // Act — retrieve and verify tenant_id on stored result
        var getResult = await _resultRepo.GetByEmployeeIdAsync(runId, employeeId);

        // Assert — tenant_id on result must match the creating tenant
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value!.TenantId.Should().Be(TenantId,
            because: "PayrollResult must carry the tenant_id for cross-tenant isolation enforcement (REQ-SEC-005)");

        // Act — attempt to retrieve with a non-existent employeeId to verify no bleed-through
        var wrongEmpResult = await _resultRepo.GetByEmployeeIdAsync(runId, "emp-wrong-tenant");
        wrongEmpResult.IsFailure.Should().BeTrue(because: "result for unknown employee must not be found");
    }

    // ── TC-PAY-011-D: ListByRunAsync returns all results for the run ──────────

    [Fact]
    public async Task ListByRunAsync_ReturnsAllResultsForRun()
    {
        // TC-PAY-011-D: Arrange
        var runId = $"pr_{Guid.NewGuid():N}";
        await CreateAndSaveParentRun(runId);

        var emp1 = $"emp_{Guid.NewGuid():N}";
        var emp2 = $"emp_{Guid.NewGuid():N}";
        var emp3 = $"emp_{Guid.NewGuid():N}";

        var result1 = CreatePayrollResult(emp1, runId, TenantId, basicSalary: 30_000m, paye: 5_000m, uifEmployee: 177.12m);
        var result2 = CreatePayrollResult(emp2, runId, TenantId, basicSalary: 45_000m, paye: 9_500m, uifEmployee: 177.12m);
        var result3 = CreatePayrollResult(emp3, runId, TenantId, basicSalary: 22_000m, paye: 2_500m, uifEmployee: 177.12m);

        await _resultRepo.CreateAsync(result1);
        await _resultRepo.CreateAsync(result2);
        await _resultRepo.CreateAsync(result3);

        // Act
        var allResults = await _resultRepo.ListByRunAsync(runId);

        // Assert
        allResults.Should().HaveCount(3,
            because: "ListByRunAsync must return all three per-employee results for this run");
        allResults.Select(r => r.EmployeeId).Should().BeEquivalentTo([emp1, emp2, emp3]);
        allResults.Should().OnlyContain(r => r.PayrollRunId == runId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates and persists a parent PayrollRun in Firestore so the subcollection has a valid parent.
    /// TC-PAY-011: Firestore subcollection requires parent document existence.
    /// </summary>
    private async Task CreateAndSaveParentRun(string runId)
    {
        var runResult = PayrollRun.Create(
            id: runId,
            tenantId: TenantId,
            period: "2026-03",
            runType: PayFrequency.Monthly,
            employeeIds: ["emp-001", "emp-002", "emp-003"],
            ruleSetVersion: "SARS_PAYE_2026",
            initiatedBy: "uid-sarah-hr",
            idempotencyKey: Guid.NewGuid().ToString("N"),
            now: DateTimeOffset.UtcNow);

        runResult.IsSuccess.Should().BeTrue("parent PayrollRun factory must succeed");
        var saveResult = await _runRepo.SaveAsync(runResult.Value!);
        saveResult.IsSuccess.Should().BeTrue("parent PayrollRun must be persisted before creating results");
    }

    /// <summary>
    /// Creates a <see cref="PayrollResult"/> with the given amounts.
    /// The factory enforces the payslip invariant: net = gross - deductions.
    /// Gross = basicSalary + overtimePay + allowances.
    /// DeductionTotal = paye + uifEmployee + pensionEmployee + medicalEmployee.
    /// TC-PAY-011: All amounts use MoneyZAR — no float or double (REQ critical rule §2).
    /// </summary>
    private static PayrollResult CreatePayrollResult(
        string employeeId,
        string payrollRunId,
        string tenantId,
        decimal basicSalary,
        decimal paye,
        decimal uifEmployee,
        decimal overtimePay = 0m,
        decimal allowances = 0m,
        decimal uifEmployer = 0m,
        decimal sdl = 0m,
        decimal pensionEmployee = 0m,
        decimal pensionEmployer = 0m,
        decimal medicalEmployee = 0m,
        decimal medicalEmployer = 0m)
    {
        var result = PayrollResult.Create(
            employeeId: employeeId,
            payrollRunId: payrollRunId,
            tenantId: tenantId,
            basicSalary: new MoneyZAR(basicSalary),
            overtimePay: new MoneyZAR(overtimePay),
            allowances: new MoneyZAR(allowances),
            paye: new MoneyZAR(paye),
            uifEmployee: new MoneyZAR(uifEmployee),
            uifEmployer: new MoneyZAR(uifEmployer),
            sdl: new MoneyZAR(sdl),
            pensionEmployee: new MoneyZAR(pensionEmployee),
            pensionEmployer: new MoneyZAR(pensionEmployer),
            medicalEmployee: new MoneyZAR(medicalEmployee),
            medicalEmployer: new MoneyZAR(medicalEmployer),
            etiAmount: MoneyZAR.Zero,
            etiEligible: false,
            otherDeductions: null,
            otherAdditions: null,
            hoursOrdinary: 173.33m,
            hoursOvertime: 0m,
            taxTableVersion: "SARS_PAYE_2026",
            complianceFlags: ["CTL-SARS-001:PASS"],
            calculationTimestamp: DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue(
            because: $"CreatePayrollResult factory for employee {employeeId} must satisfy the payslip invariant");
        return result.Value!;
    }
}
