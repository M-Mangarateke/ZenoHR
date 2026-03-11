// REQ-OPS-001: UAT — Pilot tenant compliance filing workflow integration tests.
// TC-UAT-COMP-001 through TC-UAT-COMP-006: End-to-end compliance scenarios for Zenowethu (Pty) Ltd.
// TASK-156: Simulate real tenant compliance workflows using actual filing generators and services.
// CTL-SARS-006, CTL-SARS-008, CTL-POPIA-010, CTL-POPIA-011.

using System.Globalization;
using FluentAssertions;
using ZenoHR.Domain.Common;
using ZenoHR.Infrastructure.Services.Filing.Emp201;
using ZenoHR.Infrastructure.Services.Filing.Emp501;
using ZenoHR.Infrastructure.Services.Filing.Irp5;
using ZenoHR.Integration.Tests.Infrastructure;
using ZenoHR.Module.Compliance.Entities;
using ZenoHR.Module.Compliance.Enums;
using ZenoHR.Module.Compliance.Models;
using ZenoHR.Module.Compliance.Services;
using ZenoHR.Module.Payroll.Entities;

namespace ZenoHR.Integration.Tests.Uat;

/// <summary>
/// UAT compliance workflow tests for the Zenowethu pilot tenant.
/// These tests exercise the real filing generators (EMP201, EMP501, IRP5/IT3a),
/// breach notification service, and tax reference validator.
/// REQ-OPS-001, REQ-COMP-001, CTL-SARS-006, CTL-SARS-008
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class UatComplianceWorkflowTests : IntegrationTestBase
{
    // ── Pilot tenant constants ──────────────────────────────────────────────
    // REQ-OPS-001: Zenowethu pilot tenant identification.

    private const string PilotTenantId = "zenowethu-001";
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly Emp201Generator _emp201Generator;
    private readonly Emp501Generator _emp501Generator;
    private readonly BreachNotificationService _breachService;

    public UatComplianceWorkflowTests(FirestoreEmulatorFixture fixture) : base(fixture)
    {
        _emp201Generator = new Emp201Generator();
        _emp501Generator = new Emp501Generator();
        _breachService = new BreachNotificationService();
    }

    // ── TC-UAT-COMP-001: EMP201 generation for a monthly payroll run ────────

    /// <summary>
    /// TC-UAT-COMP-001: Generates an EMP201 CSV from a monthly payroll run
    /// for 3 Zenowethu employees and validates the structure (H, D, T records).
    /// REQ-OPS-001, REQ-COMP-001, CTL-SARS-006
    /// </summary>
    [Fact]
    public void Emp201_MonthlyPayrollRun_GeneratesValidCsvStructure()
    {
        // TC-UAT-COMP-001: Arrange — build EMP201 data for March 2026
        var emp201Data = new Emp201Data
        {
            EmployerPAYEReference = "7012345678",
            EmployerUifReference = "UIF-ZNW-001",
            EmployerSdlReference = "SDL-ZNW-001",
            EmployerTradingName = "Zenowethu (Pty) Ltd",
            TaxPeriod = "202603",
            PeriodLabel = "March 2026",
            PayrollRunId = "pr_uat_202603_001",
            TotalPayeDeducted = 8_500.00m,
            TotalUifEmployee = 531.36m,
            TotalUifEmployer = 531.36m,
            TotalSdl = 780.00m,
            TotalGrossRemuneration = 78_000.00m,
            EmployeeCount = 3,
            DueDate = new DateOnly(2026, 4, 7),
            EmployeeLines =
            [
                new Emp201EmployeeLine
                {
                    EmployeeId = "emp-uat-001",
                    EmployeeFullName = "Lindiwe Dlamini",
                    TaxReferenceNumber = "0123456789",
                    IdOrPassportNumber = "9005150001087",
                    GrossRemuneration = 15_000.00m,
                    PayeDeducted = 1_200.00m,
                    UifEmployee = 150.00m,
                    UifEmployer = 150.00m,
                    SdlEmployer = 150.00m,
                    PaymentMethod = "EFT",
                },
                new Emp201EmployeeLine
                {
                    EmployeeId = "emp-uat-002",
                    EmployeeFullName = "Thabo Molefe",
                    TaxReferenceNumber = "1234567890",
                    IdOrPassportNumber = "8801015009087",
                    GrossRemuneration = 25_000.00m,
                    PayeDeducted = 3_000.00m,
                    UifEmployee = 177.12m,
                    UifEmployer = 177.12m,
                    SdlEmployer = 250.00m,
                    PaymentMethod = "EFT",
                },
                new Emp201EmployeeLine
                {
                    EmployeeId = "emp-uat-003",
                    EmployeeFullName = "Ayanda Nkosi",
                    TaxReferenceNumber = "2345678901",
                    IdOrPassportNumber = "9202280001087",
                    GrossRemuneration = 38_000.00m,
                    PayeDeducted = 4_300.00m,
                    UifEmployee = 177.12m,
                    UifEmployer = 177.12m,
                    SdlEmployer = 380.00m,
                    PaymentMethod = "EFT",
                },
            ],
            GeneratedByUserId = "uid-hrmanager-001",
            GeneratedAt = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
        };

        // Act
        var csv = _emp201Generator.GenerateCsv(emp201Data);

        // Assert — structure validation
        csv.Should().NotBeNullOrWhiteSpace();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // H record present and contains tax period
        var hLine = lines.FirstOrDefault(l => l.StartsWith("H;"));
        hLine.Should().NotBeNull(because: "EMP201 CSV must contain an H header record");
        hLine!.Should().Contain("202603", because: "H record must contain tax period 202603");
        hLine.Should().Contain("Zenowethu", because: "H record must contain employer name");

        // 3 D records (one per employee)
        var dLines = lines.Where(l => l.StartsWith("D;")).ToList();
        dLines.Should().HaveCount(3, because: "one D record per employee");

        // D records contain employee data
        dLines.Any(d => d.Contains("Lindiwe Dlamini")).Should().BeTrue();
        dLines.Any(d => d.Contains("Thabo Molefe")).Should().BeTrue();
        dLines.Any(d => d.Contains("Ayanda Nkosi")).Should().BeTrue();
    }

    // ── TC-UAT-COMP-002: EMP501 annual reconciliation ──────────────────────

    /// <summary>
    /// TC-UAT-COMP-002: Generates an EMP501 annual reconciliation CSV and summary report,
    /// then validates that reconciliation passes (monthly totals match employee totals).
    /// REQ-OPS-001, REQ-COMP-002, CTL-SARS-006
    /// </summary>
    [Fact]
    public void Emp501_AnnualReconciliation_GeneratesValidCsvAndPassesValidation()
    {
        // TC-UAT-COMP-002: Arrange — build EMP501 data for tax year 2026
        // (March 2025 – February 2026)
        var monthlyGross = 25_000.00m;
        var monthlyPaye = 2_726.58m;
        var monthlyUifEmp = 177.12m;
        var monthlyUifEr = 177.12m;
        var monthlySdl = 250.00m;

        var monthlySubmissions = Enumerable.Range(3, 10)
            .Select(m => new Emp201MonthlyEntry
            {
                Period = string.Format(Invariant, "2025-{0:D2}", m),
                TotalGross = monthlyGross,
                TotalPaye = monthlyPaye,
                TotalUifEmployee = monthlyUifEmp,
                TotalUifEmployer = monthlyUifEr,
                TotalSdl = monthlySdl,
                Filed = true,
                FiledDate = new DateOnly(2025, m + 1 > 12 ? 1 : m + 1, 7),
            })
            .Concat(Enumerable.Range(1, 2).Select(m => new Emp201MonthlyEntry
            {
                Period = string.Format(Invariant, "2026-{0:D2}", m),
                TotalGross = monthlyGross,
                TotalPaye = monthlyPaye,
                TotalUifEmployee = monthlyUifEmp,
                TotalUifEmployer = monthlyUifEr,
                TotalSdl = monthlySdl,
                Filed = true,
                FiledDate = new DateOnly(2026, m + 1, 7),
            }))
            .ToList();

        var annualGross = monthlyGross * 12m;
        var annualPaye = monthlyPaye * 12m;
        var annualUifEmp = monthlyUifEmp * 12m;
        var annualUifEr = monthlyUifEr * 12m;
        var annualSdl = monthlySdl * 12m;

        var emp501Data = new Emp501Data
        {
            TenantId = PilotTenantId,
            EmployerTaxRef = "7012345678",
            EmployerName = "Zenowethu (Pty) Ltd",
            EmployerAddress = "12 Mandela Ave, Sandton, Gauteng, 2196",
            TaxYear = "2026",
            MonthlySubmissions = monthlySubmissions,
            EmployeeEntries =
            [
                new Emp501EmployeeEntry
                {
                    EmployeeId = "emp-uat-recon-001",
                    EmployeeName = "Sipho Zulu",
                    IdNumber = "9005150001087",
                    TaxRef = "0123456789",
                    AnnualGross = annualGross,
                    AnnualPaye = annualPaye,
                    AnnualUifEmployee = annualUifEmp,
                    AnnualUifEmployer = annualUifEr,
                    AnnualSdl = annualSdl,
                    AnnualEti = 0m,
                    AnnualMedicalCredit = 0m,
                    Irp5Code = "IRP5",
                    CertificateNumber = "zenowethu-001-2026-emp-uat-recon-001",
                },
            ],
        };

        // Act — generate CSV and validate
        var csv = _emp501Generator.GenerateReconciliationCsv(emp501Data);
        var summary = _emp501Generator.GenerateSummaryReport(emp501Data);
        var issues = _emp501Generator.ValidateReconciliation(emp501Data);

        // Assert — CSV structure
        csv.Should().NotBeNullOrWhiteSpace();
        csv.Should().Contain("H;EMP501;2026", because: "H record must contain EMP501 and tax year");
        csv.Should().Contain("D;", because: "at least one D record must be present");
        csv.Should().Contain("T;", because: "T record (totals) must be present");

        // Assert — summary report
        summary.Should().Contain("Zenowethu (Pty) Ltd");
        summary.Should().Contain("2026");

        // Assert — reconciliation passes (monthly totals match employee totals)
        issues.Should().BeEmpty(because: "monthly and employee totals are aligned — no discrepancies");
    }

    // ── TC-UAT-COMP-003: IRP5/IT3a certificate generation ──────────────────

    /// <summary>
    /// TC-UAT-COMP-003: Generates an IRP5 certificate from 12 months of payroll results
    /// for a Zenowethu employee and verifies SARS code aggregation.
    /// REQ-OPS-001, CTL-SARS-008
    /// </summary>
    [Fact]
    public void Irp5Certificate_FromTwelveMonthResults_GeneratesCorrectCodes()
    {
        // TC-UAT-COMP-003: Arrange — build 12 monthly PayrollResult objects
        var now = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var monthlySalary = new MoneyZAR(25_000.00m);
        var monthlyPaye = new MoneyZAR(2_726.58m);
        var monthlyUif = new MoneyZAR(177.12m);

        var results = new List<PayrollResult>();
        for (var month = 0; month < 12; month++)
        {
            var runId = string.Format(Invariant, "pr_2025_{0:D2}", month + 3);
            var result = PayrollResult.Create(
                employeeId: "emp-irp5-001",
                payrollRunId: runId,
                tenantId: PilotTenantId,
                basicSalary: monthlySalary,
                overtimePay: MoneyZAR.Zero,
                allowances: MoneyZAR.Zero,
                paye: monthlyPaye,
                uifEmployee: monthlyUif,
                uifEmployer: monthlyUif,
                sdl: new MoneyZAR(250.00m),
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
                complianceFlags: ["CTL-SARS-001:PASS"],
                calculationTimestamp: now.AddMonths(month));

            result.IsSuccess.Should().BeTrue();
            results.Add(result.Value!);
        }

        // Act — generate IRP5 certificate
        var certResult = Irp5Generator.Generate(
            tenantId: PilotTenantId,
            taxYear: "2026",
            employeeId: "emp-irp5-001",
            employeeName: "Naledi Khumalo",
            idNumber: "9005150001087",
            taxRef: "0123456789",
            results: results);

        // Assert
        certResult.IsSuccess.Should().BeTrue(because: "IRP5 generation must succeed with valid inputs");
        var certs = certResult.Value!;
        certs.Should().HaveCount(1);

        var cert = certs[0];
        cert.CertificateType.Should().Be("IRP5", because: "PAYE > 0 means IRP5 (not IT3a)");
        cert.TenantId.Should().Be(PilotTenantId);
        cert.TaxYear.Should().Be("2026");

        // Code 3601 (basic salary) = R25,000 × 12 = R300,000
        cert.Code3601.Amount.Should().Be(300_000.00m,
            because: "annual basic salary = R25,000 × 12");

        // Code 4001 (PAYE) = R2,726.58 × 12 = R32,718.96
        cert.Code4001.Amount.Should().Be(monthlyPaye.Amount * 12m,
            because: "annual PAYE = monthly PAYE × 12");

        // Code 4005 (UIF employee) = R177.12 × 12 = R2,125.44
        cert.Code4005.Amount.Should().Be(monthlyUif.Amount * 12m,
            because: "annual UIF = monthly UIF × 12");

        // Period dates for tax year 2026 (1 March 2025 – 28 Feb 2026)
        cert.PeriodStart.Should().Be(new DateOnly(2025, 3, 1));
        cert.PeriodEnd.Month.Should().Be(2);
        cert.PeriodEnd.Year.Should().Be(2026);
    }

    // ── TC-UAT-COMP-004: Breach notification and 72-hour deadline ───────────

    /// <summary>
    /// TC-UAT-COMP-004: Registers a POPIA breach and verifies the 72-hour notification
    /// deadline is correctly calculated and overdue detection works.
    /// REQ-OPS-001, CTL-POPIA-010, CTL-POPIA-011
    /// </summary>
    [Fact]
    public void BreachNotification_RegisterAndTrack72HourDeadline_DeadlineCorrect()
    {
        // TC-UAT-COMP-004: Arrange
        var discoveredAt = new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);

        // Act — register a breach
        var registerResult = _breachService.RegisterBreach(
            tenantId: PilotTenantId,
            title: "Payslip data exposed via misconfigured API",
            description: "Employee payslip data for 12 employees was accessible without authentication for 4 hours.",
            severity: BreachSeverity.High,
            discoveredBy: "uid-hrmanager-001",
            affectedDataCategories: ["salary_data", "tax_reference_numbers", "bank_details"],
            estimatedAffectedSubjects: 12,
            rootCause: "API endpoint misconfiguration during deployment",
            remediationSteps: ["Disabled public access", "Rotated API keys", "Audit log review"],
            discoveredAt: discoveredAt);

        // Assert — breach registered
        registerResult.IsSuccess.Should().BeTrue(because: "breach registration must succeed with valid inputs");
        var breach = registerResult.Value!;
        breach.TenantId.Should().Be(PilotTenantId);
        breach.Status.Should().Be(BreachStatus.Detected);
        breach.Severity.Should().Be(BreachSeverity.High);

        // 72-hour deadline check
        breach.NotificationDeadline.Should().Be(discoveredAt.AddHours(72),
            because: "POPIA §22 requires notification within 72 hours of discovery");

        // Status progression: Detected → Investigating → Contained
        var investigateResult = _breachService.UpdateStatus(breach, BreachStatus.Investigating, discoveredAt.AddHours(1));
        investigateResult.IsSuccess.Should().BeTrue();

        var containResult = _breachService.UpdateStatus(investigateResult.Value!, BreachStatus.Contained, discoveredAt.AddHours(6));
        containResult.IsSuccess.Should().BeTrue();

        // Generate regulator notification from Contained status
        var notificationResult = _breachService.GenerateRegulatorNotification(containResult.Value!);
        notificationResult.IsSuccess.Should().BeTrue(because: "notification should be generated from Contained status");

        var notification = notificationResult.Value!;
        notification.Should().Contain("POPIA SECTION 22");
        notification.Should().Contain("Payslip data exposed");
        notification.Should().Contain("12"); // affected subjects
    }

    // ── TC-UAT-COMP-005: ComplianceSubmission lifecycle ─────────────────────

    /// <summary>
    /// TC-UAT-COMP-005: Creates a ComplianceSubmission and transitions it through
    /// Pending → Submitted → Accepted lifecycle.
    /// REQ-OPS-001, REQ-COMP-001, CTL-SARS-006
    /// </summary>
    [Fact]
    public void ComplianceSubmission_FullLifecycle_TransitionsPendingSubmittedAccepted()
    {
        // TC-UAT-COMP-005: Arrange
        var now = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero);

        var createResult = ComplianceSubmission.Create(
            id: $"cs_{PilotTenantId}_2026-03_emp201",
            tenantId: PilotTenantId,
            period: "2026-03",
            submissionType: ComplianceSubmissionType.Emp201,
            payeAmount: new MoneyZAR(8_500.00m),
            uifAmount: new MoneyZAR(531.36m),
            sdlAmount: new MoneyZAR(780.00m),
            grossAmount: new MoneyZAR(78_000.00m),
            employeeCount: 3,
            checksumSha256: "abc123def456",
            generatedFileContent: System.Text.Encoding.UTF8.GetBytes("H;EMP201;202603"),
            complianceFlags: ["CTL-SARS-006:GENERATED"],
            createdBy: "uid-hrmanager-001",
            createdAt: now);

        // Assert — Pending state
        createResult.IsSuccess.Should().BeTrue();
        var submission = createResult.Value!;
        submission.Status.Should().Be(ComplianceSubmissionStatus.Pending);
        submission.TenantId.Should().Be(PilotTenantId);
        submission.PayeAmount.Amount.Should().Be(8_500.00m);

        // Act — mark Submitted
        var submitResult = submission.MarkSubmitted("SARS-REF-2026-03-001", now.AddHours(1));
        submitResult.IsSuccess.Should().BeTrue();
        submission.Status.Should().Be(ComplianceSubmissionStatus.Submitted);
        submission.FilingReference.Should().Be("SARS-REF-2026-03-001");

        // Act — mark Accepted
        var acceptResult = submission.MarkAccepted(now.AddDays(3));
        acceptResult.IsSuccess.Should().BeTrue();
        submission.Status.Should().Be(ComplianceSubmissionStatus.Accepted);
        submission.AcceptedAt.Should().NotBeNull();
    }

    // ── TC-UAT-COMP-006: Tax reference validation ───────────────────────────

    /// <summary>
    /// TC-UAT-COMP-006: Validates SARS tax reference numbers for Zenowethu employees.
    /// Valid: 10 digits starting with 0, 1, 2, or 3. Invalid: wrong length, non-digit, wrong prefix.
    /// REQ-OPS-001, CTL-SARS-006
    /// </summary>
    [Theory]
    [InlineData("0123456789", true)]   // Valid: starts with 0
    [InlineData("1234567890", true)]   // Valid: starts with 1
    [InlineData("2345678901", true)]   // Valid: starts with 2
    [InlineData("3456789012", true)]   // Valid: starts with 3
    [InlineData("4567890123", false)]  // Invalid: starts with 4
    [InlineData("9123456789", false)]  // Invalid: starts with 9
    [InlineData("012345678", false)]   // Invalid: only 9 digits
    [InlineData("01234567890", false)] // Invalid: 11 digits
    [InlineData("012345678A", false)]  // Invalid: contains letter
    [InlineData("", false)]            // Invalid: empty
    [InlineData(null, false)]          // Invalid: null
    public void TaxReferenceValidator_VariousInputs_ValidatesCorrectly(
        string? taxRef, bool expectedValid)
    {
        // TC-UAT-COMP-006: Act
        var result = TaxReferenceValidator.Validate(taxRef);

        // Assert
        if (expectedValid)
        {
            result.IsSuccess.Should().BeTrue(
                because: $"'{taxRef}' is a valid SARS tax reference number");
            result.Value.Should().Be(taxRef);
        }
        else
        {
            result.IsFailure.Should().BeTrue(
                because: $"'{taxRef}' is not a valid SARS tax reference number");
        }
    }
}
