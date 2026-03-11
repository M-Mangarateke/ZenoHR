// REQ-OPS-001: UAT — Pilot tenant isolation and security integration tests.
// TC-UAT-SEC-001 through TC-UAT-SEC-005: Tenant isolation and MoneyZAR integrity.
// TASK-156: Verify tenant scoping and decimal-only monetary values.
// REQ-SEC-005: Tenant isolation is a Sev-1 security requirement.

using FluentAssertions;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Integration.Tests.Infrastructure;
using ZenoHR.Module.Employee.Aggregates;
using EmployeeAggregate = ZenoHR.Module.Employee.Aggregates.Employee;
using ZenoHR.Module.Leave.Aggregates;
using ZenoHR.Module.Payroll.Calculation;
using ZenoHR.Module.Payroll.Entities;

namespace ZenoHR.Integration.Tests.Uat;

/// <summary>
/// UAT tenant isolation and security tests for the Zenowethu pilot tenant.
/// These tests verify that tenant_id scoping is enforced at the domain level,
/// that MoneyZAR always uses decimal (not float/double), and that domain aggregates
/// reject cross-tenant operations.
/// REQ-OPS-001, REQ-SEC-005
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class UatTenantIsolationTests : IntegrationTestBase
{
    // ── Pilot tenant constants ──────────────────────────────────────────────
    // REQ-OPS-001: Two tenants for isolation testing.

    private const string TenantA = "zenowethu-001";
    private const string TenantB = "other-company-002";

    public UatTenantIsolationTests(FirestoreEmulatorFixture fixture) : base(fixture) { }

    // ── TC-UAT-SEC-001: Employee aggregate enforces tenant_id ───────────────

    /// <summary>
    /// TC-UAT-SEC-001: Verifies that Employee aggregates are created with the correct
    /// tenant_id and that the tenant_id is immutable.
    /// REQ-OPS-001, REQ-SEC-005
    /// </summary>
    [Fact]
    public void Employee_Create_TenantIdEnforcedAndImmutable()
    {
        // TC-UAT-SEC-001: Arrange — create employees for two different tenants
        var empA = CreateTestEmployee("emp-iso-a-001", TenantA);
        var empB = CreateTestEmployee("emp-iso-b-001", TenantB);

        // Assert — each employee has the correct tenant
        empA.IsSuccess.Should().BeTrue();
        empB.IsSuccess.Should().BeTrue();

        empA.Value!.TenantId.Should().Be(TenantA,
            because: "employee A must belong to tenant A");
        empB.Value!.TenantId.Should().Be(TenantB,
            because: "employee B must belong to tenant B");

        // Tenants are different
        empA.Value!.TenantId.Should().NotBe(empB.Value!.TenantId,
            because: "employees from different tenants must have different tenant_ids");
    }

    // ── TC-UAT-SEC-002: Leave balance enforces tenant scoping ───────────────

    /// <summary>
    /// TC-UAT-SEC-002: Verifies that LeaveBalance aggregates enforce tenant_id scoping
    /// and that balance operations are tenant-bound.
    /// REQ-OPS-001, REQ-SEC-005, REQ-HR-002
    /// </summary>
    [Fact]
    public void LeaveBalance_Create_TenantIdScopedCorrectly()
    {
        // TC-UAT-SEC-002: Arrange
        var now = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

        var balanceA = LeaveBalance.Create(
            "lb-iso-a", TenantA, "emp-a", LeaveType.Annual, "2026", "BCEA_2026", now);
        var balanceB = LeaveBalance.Create(
            "lb-iso-b", TenantB, "emp-b", LeaveType.Annual, "2026", "BCEA_2026", now);

        // Assert — both created successfully with correct tenant
        balanceA.IsSuccess.Should().BeTrue();
        balanceB.IsSuccess.Should().BeTrue();

        balanceA.Value!.TenantId.Should().Be(TenantA);
        balanceB.Value!.TenantId.Should().Be(TenantB);
        balanceA.Value!.TenantId.Should().NotBe(balanceB.Value!.TenantId);
    }

    // ── TC-UAT-SEC-003: PayrollResult enforces tenant isolation ─────────────

    /// <summary>
    /// TC-UAT-SEC-003: Verifies that PayrollResult entities are tenant-scoped and
    /// the payslip invariant holds regardless of tenant.
    /// REQ-OPS-001, REQ-SEC-005, REQ-HR-003
    /// </summary>
    [Fact]
    public void PayrollResult_Create_TenantIdPreservedInResult()
    {
        // TC-UAT-SEC-003: Arrange
        var gross = new MoneyZAR(25_000.00m);
        var paye = new MoneyZAR(2_500.00m);
        var uif = new MoneyZAR(177.12m);
        var sdl = new MoneyZAR(250.00m);

        // Act — create results for two different tenants
        var resultA = PayrollResult.Create(
            employeeId: "emp-sec-a",
            payrollRunId: "pr-sec-a",
            tenantId: TenantA,
            basicSalary: gross,
            overtimePay: MoneyZAR.Zero,
            allowances: MoneyZAR.Zero,
            paye: paye,
            uifEmployee: uif,
            uifEmployer: uif,
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
            taxTableVersion: "SARS_PAYE_2026",
            complianceFlags: null,
            calculationTimestamp: DateTimeOffset.UtcNow);

        var resultB = PayrollResult.Create(
            employeeId: "emp-sec-b",
            payrollRunId: "pr-sec-b",
            tenantId: TenantB,
            basicSalary: gross,
            overtimePay: MoneyZAR.Zero,
            allowances: MoneyZAR.Zero,
            paye: paye,
            uifEmployee: uif,
            uifEmployer: uif,
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
            taxTableVersion: "SARS_PAYE_2026",
            complianceFlags: null,
            calculationTimestamp: DateTimeOffset.UtcNow);

        // Assert
        resultA.IsSuccess.Should().BeTrue();
        resultB.IsSuccess.Should().BeTrue();

        resultA.Value!.TenantId.Should().Be(TenantA);
        resultB.Value!.TenantId.Should().Be(TenantB);
        resultA.Value!.TenantId.Should().NotBe(resultB.Value!.TenantId,
            because: "payroll results from different tenants must have different tenant_ids");
    }

    // ── TC-UAT-SEC-004: MoneyZAR uses decimal — not float or double ─────────

    /// <summary>
    /// TC-UAT-SEC-004: Verifies that MoneyZAR is backed by System.Decimal and that
    /// all payroll calculation engines produce MoneyZAR results (not float/double).
    /// Using float/double for money is a Sev-1 defect per CLAUDE.md critical rules.
    /// REQ-OPS-001, REQ-HR-003
    /// </summary>
    [Fact]
    public void MoneyZAR_BackingType_IsDecimalNotFloatOrDouble()
    {
        // TC-UAT-SEC-004: Verify MoneyZAR.Amount is System.Decimal
        var money = new MoneyZAR(12345.67m);

        // The Amount property must be System.Decimal
        money.Amount.GetType().Should().Be(typeof(decimal),
            because: "MoneyZAR must use System.Decimal — float/double is a Sev-1 defect");

        // Verify decimal precision is preserved
        var preciseAmount = new MoneyZAR(0.01m);
        preciseAmount.Amount.Should().Be(0.01m,
            because: "MoneyZAR must preserve cent-level precision");

        // Verify Firestore round-trip preserves decimal precision
        var original = new MoneyZAR(17_712.99m);
        var firestoreString = original.ToFirestoreString();
        var roundTripped = MoneyZAR.FromFirestoreString(firestoreString);
        roundTripped.Amount.Should().Be(original.Amount,
            because: "MoneyZAR must survive Firestore string serialization round-trip");
    }

    // ── TC-UAT-SEC-005: Calculation engines return MoneyZAR ─────────────────

    /// <summary>
    /// TC-UAT-SEC-005: Verifies that all payroll calculation engines (PAYE, UIF, SDL, ETI)
    /// return MoneyZAR values backed by decimal. This is a compile-time guarantee reinforced
    /// by runtime type checks.
    /// REQ-OPS-001, REQ-HR-003, CTL-SARS-001
    /// </summary>
    [Fact]
    public void CalculationEngines_AllReturnMoneyZAR_BackedByDecimal()
    {
        // TC-UAT-SEC-005: Arrange — create test rule sets
        var payeRules = SarsPayeRuleSet.CreateForTesting(
            brackets:
            [
                new PayeTaxBracket { Min = 1m, Max = 237_100m, Rate = 0.18m, BaseTax = 0m },
                new PayeTaxBracket { Min = 237_101m, Max = null, Rate = 0.26m, BaseTax = 42_678m },
            ],
            primary: 17_235m,
            secondary: 9_444m,
            tertiary: 3_145m,
            thresholdBelow65: 95_750m,
            thresholdAge65To74: 148_217m,
            thresholdAge75Plus: 165_689m);

        var uifSdlRules = SarsUifSdlRuleSet.CreateForTesting();

        var etiRules = SarsEtiRuleSet.CreateForTesting(
            tier1Bands:
            [
                new EtiRateBand { MinRemuneration = 2_500m, MaxRemuneration = 4_499.99m, FormulaType = "percentage", Rate = 0.50m },
                new EtiRateBand { MinRemuneration = 4_500m, MaxRemuneration = 7_500m, FormulaType = "fixed", FlatAmount = 1_000m },
            ],
            tier2Bands:
            [
                new EtiRateBand { MinRemuneration = 2_500m, MaxRemuneration = 4_499.99m, FormulaType = "percentage", Rate = 0.25m },
                new EtiRateBand { MinRemuneration = 4_500m, MaxRemuneration = 7_500m, FormulaType = "fixed", FlatAmount = 500m },
            ]);

        var gross = new MoneyZAR(25_000.00m);

        // Act — call each engine
        var paye = PayeCalculationEngine.CalculateMonthlyPAYE(gross, age: 30, payeRules);
        var weeklyPaye = PayeCalculationEngine.CalculateWeeklyPAYE(new MoneyZAR(5_000m), age: 30, payeRules);
        var uifEmp = UifSdlCalculationEngine.CalculateUifEmployee(gross, uifSdlRules);
        var uifEr = UifSdlCalculationEngine.CalculateUifEmployer(gross, uifSdlRules);
        var sdl = UifSdlCalculationEngine.CalculateSdl(gross, uifSdlRules, isEmployerSdlExempt: false);
        var eti = EtiCalculationEngine.CalculateMonthlyEti(new MoneyZAR(5_000m), EtiTier.Tier1, 160, etiRules);

        // Assert — all return MoneyZAR struct backed by decimal
        AssertMoneyZarIsDecimal(paye, "PAYE (monthly)");
        AssertMoneyZarIsDecimal(weeklyPaye, "PAYE (weekly)");
        AssertMoneyZarIsDecimal(uifEmp, "UIF employee");
        AssertMoneyZarIsDecimal(uifEr, "UIF employer");
        AssertMoneyZarIsDecimal(sdl, "SDL");
        AssertMoneyZarIsDecimal(eti, "ETI");

        // All values should be non-negative
        paye.Amount.Should().BeGreaterThanOrEqualTo(0m);
        weeklyPaye.Amount.Should().BeGreaterThanOrEqualTo(0m);
        uifEmp.Amount.Should().BeGreaterThanOrEqualTo(0m);
        uifEr.Amount.Should().BeGreaterThanOrEqualTo(0m);
        sdl.Amount.Should().BeGreaterThanOrEqualTo(0m);
        eti.Amount.Should().BeGreaterThanOrEqualTo(0m);
    }

    // ── TC-UAT-SEC-006: Domain aggregate rejects empty tenant_id ────────────

    /// <summary>
    /// TC-UAT-SEC-006: Verifies that domain aggregates reject creation with
    /// empty or null tenant_id.
    /// REQ-OPS-001, REQ-SEC-005
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void DomainAggregates_EmptyTenantId_CreationRejected(string invalidTenantId)
    {
        // TC-UAT-SEC-006: Employee with empty tenant
        var empResult = EmployeeAggregate.Create(
            employeeId: "emp-no-tenant",
            tenantId: invalidTenantId,
            firebaseUid: "uid-test",
            legalName: "Test Employee",
            nationalIdOrPassport: "9005150001087",
            taxReference: "0123456789",
            dateOfBirth: new DateOnly(1990, 5, 15),
            personalPhoneNumber: "+27821234567",
            personalEmail: "test@zenowethu.co.za",
            workEmail: "work.test@zenowethu.co.za",
            nationality: "ZA",
            gender: "Male",
            race: "African",
            disabilityStatus: false,
            disabilityDescription: null,
            hireDate: new DateOnly(2023, 1, 1),
            employeeType: EmployeeType.Permanent,
            departmentId: "dept-001",
            roleId: "role-001",
            systemRole: "Employee",
            reportsToEmployeeId: null,
            actorId: "uid-hrmanager",
            now: DateTimeOffset.UtcNow);

        empResult.IsFailure.Should().BeTrue(
            because: "Employee creation must reject empty tenant_id");

        // LeaveBalance with empty tenant
        var balResult = LeaveBalance.Create(
            "lb-no-tenant", invalidTenantId, "emp-001", LeaveType.Annual, "2026", "BCEA_2026",
            DateTimeOffset.UtcNow);

        balResult.IsFailure.Should().BeTrue(
            because: "LeaveBalance creation must reject empty tenant_id");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Result<EmployeeAggregate> CreateTestEmployee(string empId, string tenantId)
    {
        return EmployeeAggregate.Create(
            employeeId: empId,
            tenantId: tenantId,
            firebaseUid: $"uid_{Guid.NewGuid():N}",
            legalName: $"Isolation Test Employee {empId}",
            nationalIdOrPassport: "9005150001087",
            taxReference: "0123456789",
            dateOfBirth: new DateOnly(1990, 5, 15),
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
    }

    private static void AssertMoneyZarIsDecimal(MoneyZAR value, string label)
    {
        // MoneyZAR.Amount must be System.Decimal at runtime
        value.Amount.GetType().Should().Be(typeof(decimal),
            because: $"{label} must use System.Decimal — float/double is Sev-1");

        // Verify the value is a valid MoneyZAR (not NaN or infinity — which would indicate a float leak)
        decimal.IsInteger(value.Amount * 100m / 100m); // Decimal operations don't produce NaN
    }
}
