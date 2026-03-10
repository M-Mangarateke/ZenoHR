// TC-COMP-IRP5: IRP5/IT3(a) certificate generator and XML serializer tests.
// CTL-SARS-008: Annual tax certificate generation per employee per tax year.
// Tests cover: certificate type (IRP5/IT3a), annual aggregation, deterministic numbering,
// tax year period boundaries (including leap year), XML structure and monetary formatting.

using FluentAssertions;
using System.Xml.Linq;
using ZenoHR.Domain.Common;
using ZenoHR.Infrastructure.Services.Filing.Irp5;
using ZenoHR.Module.Payroll.Entities;

namespace ZenoHR.Module.Compliance.Tests.Irp5;

/// <summary>
/// Tests for <see cref="Irp5Generator"/> and <see cref="Irp5XmlSerializer"/> covering:
/// - IRP5 vs IT3(a) certificate type selection
/// - Annual aggregation of all SARS codes across 12 monthly results
/// - Deterministic certificate number
/// - Validation failures (empty results, null/blank inputs)
/// - SA tax year period boundaries (standard and leap year)
/// - XML structure, element count, and monetary formatting
/// </summary>
public sealed class Irp5GeneratorTests
{

    // ── Fixture helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a single monthly PayrollResult with the given parameters.
    /// Uses PayrollResult.Create (factory with invariant check).
    /// Basic salary, allowances, and additions must produce consistent net_pay.
    /// </summary>
    private static PayrollResult MakeResult(
        string employeeId = "emp-001",
        string tenantId   = "tenant-zenowethu",
        decimal basicSalary    = 30_000m,
        decimal allowances     = 1_000m,
        decimal paye           = 5_000m,
        decimal uifEmployee    = 177.12m,
        decimal pensionEmployee = 1_500m,
        decimal medicalEmployee = 500m,
        decimal otherDeduction  = 0m,
        decimal otherAddition   = 0m)
    {
        var otherDeductions = otherDeduction > 0m
            ? new List<OtherLineItem> { new("CODE-LOAN", "Loan Repayment", otherDeduction) }
            : null;
        var otherAdditions = otherAddition > 0m
            ? new List<OtherLineItem> { new("CODE-REIMB", "Reimbursement", otherAddition) }
            : null;

        var result = PayrollResult.Create(
            employeeId:        employeeId,
            payrollRunId:      $"pr-{Guid.NewGuid():N}",
            tenantId:          tenantId,
            basicSalary:       new MoneyZAR(basicSalary),
            overtimePay:       MoneyZAR.Zero,
            allowances:        new MoneyZAR(allowances),
            paye:              new MoneyZAR(paye),
            uifEmployee:       new MoneyZAR(uifEmployee),
            uifEmployer:       new MoneyZAR(177.12m),
            sdl:               new MoneyZAR(300m),
            pensionEmployee:   new MoneyZAR(pensionEmployee),
            pensionEmployer:   MoneyZAR.Zero,
            medicalEmployee:   new MoneyZAR(medicalEmployee),
            medicalEmployer:   MoneyZAR.Zero,
            etiAmount:         MoneyZAR.Zero,
            etiEligible:       false,
            otherDeductions:   otherDeductions,
            otherAdditions:    otherAdditions,
            hoursOrdinary:     176m,
            hoursOvertime:     0m,
            taxTableVersion:   "v2026.1.0",
            complianceFlags:   null,
            calculationTimestamp: DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue("test fixture must produce a valid PayrollResult");
        return result.Value;
    }

    /// <summary>Creates 12 identical monthly results for a full tax year.</summary>
    private static List<PayrollResult> MakeTwelveMonths(
        string employeeId      = "emp-001",
        decimal basicSalary    = 30_000m,
        decimal allowances     = 1_000m,
        decimal paye           = 5_000m,
        decimal uifEmployee    = 177.12m,
        decimal pensionEmployee = 1_500m,
        decimal medicalEmployee = 500m,
        decimal otherDeduction  = 0m,
        decimal otherAddition   = 0m)
    {
        return Enumerable.Range(1, 12)
            .Select(_ => MakeResult(
                employeeId:         employeeId,
                basicSalary:        basicSalary,
                allowances:         allowances,
                paye:               paye,
                uifEmployee:        uifEmployee,
                pensionEmployee:    pensionEmployee,
                medicalEmployee:    medicalEmployee,
                otherDeduction:     otherDeduction,
                otherAddition:      otherAddition))
            .ToList();
    }

    // ── IRP5 vs IT3(a) type selection ─────────────────────────────────────────

    [Fact]
    public void Generate_SingleEmployee_FullYear_CreatesIrp5Certificate()
    {
        // TC-COMP-IRP5-001 — employee with PAYE > 0 → IRP5 type
        // CTL-SARS-008
        var results = MakeTwelveMonths(paye: 5_000m);

        var outcome = Irp5Generator.Generate(
            "tenant-zenowethu", "2026", "emp-001",
            "Thabo Nkosi", "8501015800085", "1234567890",
            results);

        outcome.IsSuccess.Should().BeTrue();
        var certs = outcome.Value;
        certs.Should().HaveCount(1);
        certs[0].CertificateType.Should().Be("IRP5");
        certs[0].Code4001.Amount.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void Generate_EmployeeBelowTaxThreshold_CreatesIT3aCertificate()
    {
        // TC-COMP-IRP5-002 — no PAYE deducted → IT3(a) type
        // CTL-SARS-008: Below-threshold employees still need an IT3(a) certificate.
        var results = MakeTwelveMonths(paye: 0m);

        var outcome = Irp5Generator.Generate(
            "tenant-zenowethu", "2026", "emp-002",
            "Zanele Dube", "9203074800081", "9876543210",
            results);

        outcome.IsSuccess.Should().BeTrue();
        var certs = outcome.Value;
        certs.Should().HaveCount(1);
        certs[0].CertificateType.Should().Be("IT3a");
        certs[0].Code4001.Amount.Should().Be(0m);
    }

    // ── Annual aggregation ────────────────────────────────────────────────────

    [Fact]
    public void Generate_AggregatesAllMonthlyResults_SumsCodesCorrectly()
    {
        // TC-COMP-IRP5-003 — 12 months, verify every SARS code is summed correctly.
        // CTL-SARS-008: Annual totals = monthly values × 12 for uniform monthly pay.
        const decimal basicSalary     = 25_000m;
        const decimal allowances      = 2_000m;
        const decimal paye            = 4_500m;
        const decimal uifEmployee     = 177.12m;
        const decimal pensionEmployee = 1_250m;
        const decimal medicalEmployee = 750m;
        const decimal otherDeduction  = 500m;
        const decimal otherAddition   = 200m;

        var results = MakeTwelveMonths(
            basicSalary:     basicSalary,
            allowances:      allowances,
            paye:            paye,
            uifEmployee:     uifEmployee,
            pensionEmployee: pensionEmployee,
            medicalEmployee: medicalEmployee,
            otherDeduction:  otherDeduction,
            otherAddition:   otherAddition);

        var outcome = Irp5Generator.Generate(
            "tenant-zenowethu", "2026", "emp-003",
            "Sipho Mthembu", "8803064800083", "1111111111",
            results);

        outcome.IsSuccess.Should().BeTrue();
        var cert = outcome.Value[0];

        cert.Code3601.Amount.Should().Be(basicSalary * 12);
        cert.Code3605.Amount.Should().Be(0m); // bonus not tracked in v1
        cert.Code3713.Amount.Should().Be(allowances * 12);
        cert.Code3697.Amount.Should().Be(otherAddition * 12);
        cert.Code4001.Amount.Should().Be(paye * 12);
        cert.Code4005.Amount.Should().Be(uifEmployee * 12);
        cert.Code4474.Amount.Should().Be(pensionEmployee * 12);
        cert.Code4493.Amount.Should().Be(medicalEmployee * 12);
        cert.Code4497.Amount.Should().Be(otherDeduction * 12);

        var expectedTotal = (basicSalary + allowances + otherAddition) * 12;
        cert.TotalRemuneration.Amount.Should().Be(expectedTotal);
        cert.TaxableIncome.Amount.Should().Be(expectedTotal);
    }

    // ── Deterministic certificate number ─────────────────────────────────────

    [Fact]
    public void Generate_CertificateNumber_IsDeterministic()
    {
        // TC-COMP-IRP5-004 — same inputs always produce the same certificate number.
        // CTL-SARS-008: Determinism required for reconciliation integrity.
        var results1 = MakeTwelveMonths();
        var results2 = MakeTwelveMonths();

        var outcome1 = Irp5Generator.Generate(
            "tenant-zenowethu", "2026", "emp-001",
            "Thabo Nkosi", "8501015800085", "1234567890", results1);
        var outcome2 = Irp5Generator.Generate(
            "tenant-zenowethu", "2026", "emp-001",
            "Thabo Nkosi", "8501015800085", "1234567890", results2);

        outcome1.IsSuccess.Should().BeTrue();
        outcome2.IsSuccess.Should().BeTrue();

        outcome1.Value[0].CertificateNumber
            .Should().Be(outcome2.Value[0].CertificateNumber);

        // Format: {tenantId}-{taxYear}-{employeeId}
        outcome1.Value[0].CertificateNumber
            .Should().Be("tenant-zenowethu-2026-emp-001");
    }

    // ── Validation failures ───────────────────────────────────────────────────

    [Fact]
    public void Generate_EmptyResults_ReturnsFailure()
    {
        // TC-COMP-IRP5-005 — no payroll results → failure result.
        // CTL-SARS-008: Cannot generate a certificate with no payroll data.
        var outcome = Irp5Generator.Generate(
            "tenant-zenowethu", "2026", "emp-001",
            "Thabo Nkosi", "8501015800085", "1234567890",
            new List<PayrollResult>());

        outcome.IsFailure.Should().BeTrue();
        outcome.Error.Code.Should().Be(ZenoHR.Domain.Errors.ZenoHrErrorCode.ValidationFailed);
    }

    [Fact]
    public void Generate_NullOrBlankTenantId_ReturnsFailure()
    {
        // TC-COMP-IRP5-006 — blank tenantId → validation failure.
        var results = MakeTwelveMonths();
        var outcome = Irp5Generator.Generate(
            "", "2026", "emp-001",
            "Thabo Nkosi", "8501015800085", "1234567890", results);
        outcome.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Generate_InvalidTaxYear_ReturnsFailure()
    {
        // TC-COMP-IRP5-007 — non-numeric taxYear → validation failure.
        var results = MakeTwelveMonths();
        var outcome = Irp5Generator.Generate(
            "tenant-zenowethu", "INVALID", "emp-001",
            "Thabo Nkosi", "8501015800085", "1234567890", results);
        outcome.IsFailure.Should().BeTrue();
    }

    // ── Tax year period boundary ──────────────────────────────────────────────

    [Fact]
    public void Generate_TaxYearPeriod_IsCorrect()
    {
        // TC-COMP-IRP5-008 — TaxYear "2026" → 2025-03-01 to 2026-02-28.
        // CTL-SARS-008: SA tax year always starts 1 March previous year, ends last Feb.
        var results = MakeTwelveMonths();

        var outcome = Irp5Generator.Generate(
            "tenant-zenowethu", "2026", "emp-001",
            "Thabo Nkosi", "8501015800085", "1234567890", results);

        outcome.IsSuccess.Should().BeTrue();
        var cert = outcome.Value[0];
        cert.PeriodStart.Should().Be(new DateOnly(2025, 3, 1));
        cert.PeriodEnd.Should().Be(new DateOnly(2026, 2, 28));
    }

    [Fact]
    public void Generate_TaxYearPeriod_LeapYear_IsCorrect()
    {
        // TC-COMP-IRP5-009 — TaxYear "2028" → 2027-03-01 to 2028-02-29 (2028 is leap year).
        // CTL-SARS-008: Leap year handling — February has 29 days in 2028.
        var results = MakeTwelveMonths();

        var outcome = Irp5Generator.Generate(
            "tenant-zenowethu", "2028", "emp-001",
            "Thabo Nkosi", "8501015800085", "1234567890", results);

        outcome.IsSuccess.Should().BeTrue();
        var cert = outcome.Value[0];
        cert.PeriodStart.Should().Be(new DateOnly(2027, 3, 1));
        cert.PeriodEnd.Should().Be(new DateOnly(2028, 2, 29));
    }

    // ── XML serializer ────────────────────────────────────────────────────────

    [Fact]
    public void Serialize_MultipleEmployees_ProducesValidXml()
    {
        // TC-COMP-IRP5-010 — two certificates → valid XML with Count=2 attribute.
        // CTL-SARS-008: XML output is the SARS submission artifact.
        var results1 = MakeTwelveMonths("emp-001", paye: 5_000m);
        var results2 = MakeTwelveMonths("emp-002", paye: 0m);

        var cert1 = Irp5Generator.Generate(
            "tenant-zenowethu", "2026", "emp-001",
            "Thabo Nkosi", "8501015800085", "1234567890", results1).Value[0];
        var cert2 = Irp5Generator.Generate(
            "tenant-zenowethu", "2026", "emp-002",
            "Zanele Dube", "9203074800081", "9876543210", results2).Value[0];

        var certificates = new List<Irp5Certificate> { cert1, cert2 };
        var xml = Irp5XmlSerializer.Serialize("tenant-zenowethu", "2026", certificates);

        // Parse and verify structure.
        var doc = XDocument.Parse(xml);
        XNamespace ns = "http://www.sars.gov.za/irp5";

        var root = doc.Root;
        root.Should().NotBeNull();
        root!.Name.Should().Be(ns + "IRP5IT3aFile");
        root.Attribute("TaxYear")?.Value.Should().Be("2026");

        var certsElem = root.Element(ns + "Certificates");
        certsElem.Should().NotBeNull();
        certsElem!.Attribute("Count")?.Value.Should().Be("2");

        var certElems = certsElem.Elements(ns + "Certificate").ToList();
        certElems.Should().HaveCount(2);

        // First cert is IRP5 (PAYE > 0).
        certElems[0].Attribute("Type")?.Value.Should().Be("IRP5");
        // Second cert is IT3a (no PAYE).
        certElems[1].Attribute("Type")?.Value.Should().Be("IT3a");
    }

    [Fact]
    public void Serialize_MonetaryAmounts_FormattedCorrectly()
    {
        // TC-COMP-IRP5-011 — R1234.56 serializes as "1234.56" (no currency symbol, 2dp, invariant).
        // CTL-SARS-008, CA1305: InvariantCulture decimal formatting for SARS submission.
        var results = MakeTwelveMonths(
            basicSalary:  1_234.56m / 12m * 12m, // ensure exact value survives round-trip
            paye:         100m,
            allowances:   0m,
            pensionEmployee: 0m,
            medicalEmployee: 0m,
            uifEmployee:  0m);

        // Override with a predictable value by using a simple single-month result.
        var singleResult = MakeResult(
            basicSalary: 1_234.56m,
            paye: 100m,
            allowances: 0m,
            pensionEmployee: 0m,
            medicalEmployee: 0m,
            uifEmployee: 0m);

        var cert = Irp5Generator.Generate(
            "tenant-zenowethu", "2026", "emp-001",
            "Thabo Nkosi", "8501015800085", "1234567890",
            new List<PayrollResult> { singleResult }).Value[0];

        var xml = Irp5XmlSerializer.Serialize("tenant-zenowethu", "2026",
            new List<Irp5Certificate> { cert });

        // Verify Code3601 (basic salary) appears as "1234.56" — no R prefix, no comma separator.
        XNamespace ns = "http://www.sars.gov.za/irp5";
        var doc = XDocument.Parse(xml);
        var code3601 = doc.Descendants(ns + "Code3601").FirstOrDefault();
        code3601.Should().NotBeNull();
        code3601!.Value.Should().Be("1234.56");

        // Verify Code4001 (PAYE) is "100.00".
        var code4001 = doc.Descendants(ns + "Code4001").FirstOrDefault();
        code4001.Should().NotBeNull();
        code4001!.Value.Should().Be("100.00");
    }
}
