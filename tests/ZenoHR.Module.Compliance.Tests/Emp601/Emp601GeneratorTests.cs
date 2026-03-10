// TC-COMP-EMP601: EMP601 certificate cancellation declaration generator tests.
// CTL-SARS-005: Verifies CSV structure (H/D/T records), trailer totals, validation failures,
// null/present replacement certificate handling, and InvariantCulture monetary formatting.

using FluentAssertions;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Infrastructure.Services.Filing.Emp601;

namespace ZenoHR.Module.Compliance.Tests.Emp601;

/// <summary>
/// Tests for <see cref="Emp601Generator"/> covering:
/// - CSV structure validation (H, D, T records present)
/// - Trailer totals aggregation across multiple records
/// - Null vs present replacement certificate number
/// - Input validation (empty records, blank tenantId, invalid taxYear)
/// - Monetary formatting with InvariantCulture
/// - Date formatting
/// </summary>
public sealed class Emp601GeneratorTests
{
    private static readonly DateTimeOffset _generatedAt =
        new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);

    // ── Fixture helpers ───────────────────────────────────────────────────────

    private static Emp601Record MakeRecord(
        string employeeId = "emp-001",
        string employeeName = "Sarah Nkosi",
        string idNumber = "8801015800082",
        string taxRef = "9876543210",
        string originalCertNumber = "IRP5-2025-001",
        string cancellationReason = "DATA_ERROR",
        string taxYear = "2026",
        DateOnly? cancellationDate = null,
        decimal originalPaye = 5_000m,
        decimal originalGross = 30_000m,
        string? replacementCertNumber = null) =>
        new Emp601Record(
            EmployeeId: employeeId,
            EmployeeName: employeeName,
            IdNumber: idNumber,
            TaxReferenceNumber: taxRef,
            OriginalCertificateNumber: originalCertNumber,
            CancellationReason: cancellationReason,
            TaxYear: taxYear,
            CancellationDate: cancellationDate ?? new DateOnly(2026, 3, 10),
            OriginalPayeAmount: new MoneyZAR(originalPaye),
            OriginalGrossAmount: new MoneyZAR(originalGross),
            ReplacementCertificateNumber: replacementCertNumber);

    // ── Generate_SingleRecord_ProducesValidCsv ────────────────────────────────

    [Fact]
    public void Generate_SingleRecord_ProducesValidCsv()
    {
        // TC-COMP-EMP601-001: A single record must produce H, D, and T lines.
        // CTL-SARS-005: H record count must equal 1.
        var records = new List<Emp601Record> { MakeRecord() };

        var result = Emp601Generator.Generate("tenant-zenowethu", "2026", records, _generatedAt);

        result.IsSuccess.Should().BeTrue();
        var lines = result.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var hLine = lines.FirstOrDefault(l => l.StartsWith("H;"));
        hLine.Should().NotBeNull("CSV must contain an H record");
        hLine.Should().Contain("EMP601");
        hLine.Should().Contain("2026");
        hLine.Should().Contain("tenant-zenowethu");
        hLine.Should().Contain(";1", "H record must include record count of 1");

        var dLines = lines.Where(l => l.StartsWith("D;")).ToList();
        dLines.Should().HaveCount(1, "one D record per cancellation entry");

        var tLine = lines.FirstOrDefault(l => l.StartsWith("T;"));
        tLine.Should().NotBeNull("CSV must contain a T record");
    }

    // ── Generate_MultipleRecords_TrailerTotalsCorrect ─────────────────────────

    [Fact]
    public void Generate_MultipleRecords_TrailerTotalsCorrect()
    {
        // TC-COMP-EMP601-002: Trailer T record must contain sum of all PAYE and Gross amounts.
        // CTL-SARS-005: totalOriginalPaye = 5000 + 8000 + 3500 = 16500.00
        //               totalOriginalGross = 30000 + 50000 + 25000 = 105000.00
        var records = new List<Emp601Record>
        {
            MakeRecord(employeeId: "emp-001", originalPaye: 5_000m,  originalGross: 30_000m),
            MakeRecord(employeeId: "emp-002", originalPaye: 8_000m,  originalGross: 50_000m),
            MakeRecord(employeeId: "emp-003", originalPaye: 3_500m,  originalGross: 25_000m),
        };

        var result = Emp601Generator.Generate("tenant-zenowethu", "2026", records, _generatedAt);

        result.IsSuccess.Should().BeTrue();
        var lines = result.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var tLine = lines.FirstOrDefault(l => l.StartsWith("T;"));
        tLine.Should().NotBeNull();
        tLine.Should().Contain(";3;", "T record must contain record count of 3");
        tLine.Should().Contain("16500.00", "T record must contain total PAYE of 16500.00");
        tLine.Should().Contain("105000.00", "T record must contain total gross of 105000.00");

        var dLines = lines.Where(l => l.StartsWith("D;")).ToList();
        dLines.Should().HaveCount(3, "one D line per record");
    }

    // ── Generate_NullReplacementCert_OutputsEmptyField ────────────────────────

    [Fact]
    public void Generate_NullReplacementCert_OutputsEmptyField()
    {
        // TC-COMP-EMP601-003: ReplacementCertificateNumber null → empty field at end of D line.
        // CTL-SARS-005: Last field in D record is empty when no replacement cert is issued.
        var records = new List<Emp601Record>
        {
            MakeRecord(replacementCertNumber: null)
        };

        var result = Emp601Generator.Generate("tenant-zenowethu", "2026", records, _generatedAt);

        result.IsSuccess.Should().BeTrue();
        var lines = result.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var dLine = lines.First(l => l.StartsWith("D;")).TrimEnd('\r');

        // D line ends with a semicolon followed by empty string (last field is blank)
        dLine.Should().EndWith(";", "D line must end with semicolon when replacement cert is null");
    }

    // ── Generate_WithReplacementCert_OutputsCertNumber ────────────────────────

    [Fact]
    public void Generate_WithReplacementCert_OutputsCertNumber()
    {
        // TC-COMP-EMP601-004: ReplacementCertificateNumber present → cert number appears in D line.
        // CTL-SARS-005: Replacement certificate number must be in last field of D record.
        var records = new List<Emp601Record>
        {
            MakeRecord(replacementCertNumber: "IRP5-2026-001-REP")
        };

        var result = Emp601Generator.Generate("tenant-zenowethu", "2026", records, _generatedAt);

        result.IsSuccess.Should().BeTrue();
        var lines = result.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var dLine = lines.First(l => l.StartsWith("D;")).TrimEnd('\r');

        dLine.Should().Contain("IRP5-2026-001-REP", "replacement certificate number must appear in D line");
        dLine.Should().EndWith("IRP5-2026-001-REP", "replacement cert number is the last field in D line");
    }

    // ── Generate_EmptyRecords_ReturnsFailure ──────────────────────────────────

    [Fact]
    public void Generate_EmptyRecords_ReturnsFailure()
    {
        // TC-COMP-EMP601-005: Empty records list → Result failure with ValidationFailed code.
        // CTL-SARS-005: EMP601 must have at least one cancellation record.
        var result = Emp601Generator.Generate(
            "tenant-zenowethu", "2026",
            new List<Emp601Record>(),
            _generatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    // ── Generate_EmptyTenantId_ReturnsFailure ─────────────────────────────────

    [Fact]
    public void Generate_EmptyTenantId_ReturnsFailure()
    {
        // TC-COMP-EMP601-006: Blank tenantId → Result failure.
        // CTL-SARS-005: tenantId is mandatory for tenant isolation.
        var records = new List<Emp601Record> { MakeRecord() };

        var result = Emp601Generator.Generate("", "2026", records, _generatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    // ── Generate_InvalidTaxYear_ReturnsFailure ────────────────────────────────

    [Fact]
    public void Generate_InvalidTaxYear_ReturnsFailure()
    {
        // TC-COMP-EMP601-007: Non-numeric or wrong-length taxYear → Result failure.
        // CTL-SARS-005: taxYear must be exactly 4 ASCII digits.
        var records = new List<Emp601Record> { MakeRecord() };

        var result = Emp601Generator.Generate("tenant-zenowethu", "ABCD", records, _generatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.InvalidFormat);
    }

    // ── Generate_MonetaryAmounts_FormattedWithInvariantCulture ───────────────

    [Fact]
    public void Generate_MonetaryAmounts_FormattedWithInvariantCulture()
    {
        // TC-COMP-EMP601-008: R1234.56 must appear as "1234.56" — no currency symbol, no comma separator.
        // CA1305 + CTL-SARS-005: InvariantCulture formatting prevents locale-specific decimal separators.
        var records = new List<Emp601Record>
        {
            MakeRecord(originalPaye: 1_234.56m, originalGross: 98_765.43m)
        };

        var result = Emp601Generator.Generate("tenant-zenowethu", "2026", records, _generatedAt);

        result.IsSuccess.Should().BeTrue();
        var csv = result.Value;

        csv.Should().Contain("1234.56", "PAYE amount must be formatted as 1234.56 (invariant)");
        csv.Should().Contain("98765.43", "Gross amount must be formatted as 98765.43 (invariant)");
        csv.Should().NotContain("R ", "no currency symbol must appear in CSV output");
    }

    // ── Generate_CancellationDate_FormattedCorrectly ──────────────────────────

    [Fact]
    public void Generate_CancellationDate_FormattedCorrectly()
    {
        // TC-COMP-EMP601-009: DateOnly(2026, 3, 15) must appear as "2026-03-15" in D line.
        // CTL-SARS-005: SARS e@syFile date format is yyyy-MM-dd.
        var records = new List<Emp601Record>
        {
            MakeRecord(cancellationDate: new DateOnly(2026, 3, 15))
        };

        var result = Emp601Generator.Generate("tenant-zenowethu", "2026", records, _generatedAt);

        result.IsSuccess.Should().BeTrue();
        var lines = result.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var dLine = lines.First(l => l.StartsWith("D;"));

        dLine.Should().Contain("2026-03-15", "cancellation date must be formatted as yyyy-MM-dd");
    }
}
