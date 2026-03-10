// CTL-SARS-004: EMP701 prior-year reconciliation adjustment generator tests.
// TC-COMP-EMP701: Covers CSV structure (H/D/T records), trailer totals, negative differences,
// null notes, invariant culture formatting, and input validation failures.

using FluentAssertions;
using ZenoHR.Domain.Common;
using ZenoHR.Infrastructure.Services.Filing.Emp701;

namespace ZenoHR.Module.Compliance.Tests.Emp701;

/// <summary>
/// Tests for <see cref="Emp701Generator"/> covering:
/// - CSV record structure (H, D, T records)
/// - Trailer aggregate totals across multiple records
/// - Negative differences (overpayment corrections)
/// - Null and non-null notes handling
/// - Input validation (empty tenantId, empty records, invalid taxYear)
/// - InvariantCulture monetary formatting
/// - Date formatting
/// - Computed difference properties on <see cref="Emp701AdjustmentRecord"/>
/// </summary>
public sealed class Emp701GeneratorTests
{
    // ── Test fixtures ──────────────────────────────────────────────────────────

    private static readonly DateTimeOffset FixedGeneratedAt =
        new(2026, 2, 28, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Split CSV output into trimmed lines, removing blank entries.
    /// AppendLine uses Environment.NewLine (\r\n on Windows); split on \n and trim \r.
    /// </summary>
    private static string[] SplitLines(string csv) =>
        csv.Split('\n', StringSplitOptions.RemoveEmptyEntries)
           .Select(l => l.TrimEnd('\r'))
           .Where(l => l.Length > 0)
           .ToArray();

    private static Emp701AdjustmentRecord CreateRecord(
        MoneyZAR originalPaye,
        MoneyZAR adjustedPaye,
        MoneyZAR originalGross,
        MoneyZAR adjustedGross,
        MoneyZAR originalUif,
        MoneyZAR adjustedUif,
        DateOnly? adjustmentDate = null,
        string? notes = null) => new(
            EmployeeId:               "emp-001",
            EmployeeName:             "Sarah Nkosi",
            IdNumber:                 "8801015800082",
            TaxReferenceNumber:       "9876543210",
            OriginalCertificateNumber: "IRP5-2025-001",
            AdjustmentReason:         "PAYE_CALCULATION_ERROR",
            TaxYear:                  "2025",
            OriginalPayeAmount:       originalPaye,
            AdjustedPayeAmount:       adjustedPaye,
            OriginalGrossAmount:      originalGross,
            AdjustedGrossAmount:      adjustedGross,
            OriginalUifAmount:        originalUif,
            AdjustedUifAmount:        adjustedUif,
            AdjustmentDate:           adjustmentDate ?? new DateOnly(2026, 2, 28),
            Notes:                    notes);

    private static Emp701AdjustmentRecord DefaultRecord(string? notes = null) =>
        CreateRecord(
            originalPaye:   new MoneyZAR(10_000m),
            adjustedPaye:   new MoneyZAR(11_000m),
            originalGross:  new MoneyZAR(50_000m),
            adjustedGross:  new MoneyZAR(52_000m),
            originalUif:    new MoneyZAR(177.12m),
            adjustedUif:    new MoneyZAR(177.12m),
            notes:          notes);

    // ── Generate_SingleRecord_ProducesValidCsv ────────────────────────────────

    [Fact]
    public void Generate_SingleRecord_ProducesValidCsv()
    {
        // TC-COMP-EMP701-001: H, D, and T records must be present with correct record type prefixes.
        var result = Emp701Generator.Generate("tenant-zenowethu", "2025",
            new[] { DefaultRecord() }, FixedGeneratedAt);

        result.IsSuccess.Should().BeTrue();
        var lines = SplitLines(result.Value);

        var hLine = lines.FirstOrDefault(l => l.StartsWith("H;"));
        var dLines = lines.Where(l => l.StartsWith("D;")).ToList();
        var tLine = lines.FirstOrDefault(l => l.StartsWith("T;"));

        hLine.Should().NotBeNull("CSV must have an H header record");
        hLine.Should().Contain("EMP701");
        hLine.Should().Contain("2025");
        hLine.Should().Contain("tenant-zenowethu");
        hLine.Should().Contain("2026-02-28");
        hLine.Should().Contain(";1");

        dLines.Should().HaveCount(1, "one D record per adjustment record");

        tLine.Should().NotBeNull("CSV must have a T trailer record");
    }

    // ── Generate_MultipleRecords_TrailerTotalsCorrect ─────────────────────────

    [Fact]
    public void Generate_MultipleRecords_TrailerTotalsCorrect()
    {
        // TC-COMP-EMP701-002: T record must sum PayeDifference, GrossDifference, UifDifference
        // across all records.
        // Record 1: PAYE diff = +1000, Gross diff = +2000, UIF diff = 0
        // Record 2: PAYE diff = +500,  Gross diff = +1500, UIF diff = +10
        // Record 3: PAYE diff = -200,  Gross diff = 0,     UIF diff = -5
        // Totals:   PAYE = 1300,       Gross = 3500,       UIF = 5
        var record1 = CreateRecord(
            originalPaye:  new MoneyZAR(10_000m), adjustedPaye: new MoneyZAR(11_000m),
            originalGross: new MoneyZAR(50_000m), adjustedGross: new MoneyZAR(52_000m),
            originalUif:   new MoneyZAR(177.12m), adjustedUif:  new MoneyZAR(177.12m));

        var record2 = new Emp701AdjustmentRecord(
            EmployeeId: "emp-002", EmployeeName: "Thabo Molefe", IdNumber: "9001015800083",
            TaxReferenceNumber: "1111111111", OriginalCertificateNumber: "IRP5-2025-002",
            AdjustmentReason: "BONUS_OMITTED", TaxYear: "2025",
            OriginalPayeAmount:  new MoneyZAR(5_000m),
            AdjustedPayeAmount:  new MoneyZAR(5_500m),
            OriginalGrossAmount: new MoneyZAR(30_000m),
            AdjustedGrossAmount: new MoneyZAR(31_500m),
            OriginalUifAmount:   new MoneyZAR(100m),
            AdjustedUifAmount:   new MoneyZAR(110m),
            AdjustmentDate: new DateOnly(2026, 2, 28), Notes: null);

        var record3 = new Emp701AdjustmentRecord(
            EmployeeId: "emp-003", EmployeeName: "Nomsa Dlamini", IdNumber: "9201015800084",
            TaxReferenceNumber: "2222222222", OriginalCertificateNumber: "IRP5-2025-003",
            AdjustmentReason: "UIF_SHORTFALL", TaxYear: "2025",
            OriginalPayeAmount:  new MoneyZAR(8_000m),
            AdjustedPayeAmount:  new MoneyZAR(7_800m),
            OriginalGrossAmount: new MoneyZAR(40_000m),
            AdjustedGrossAmount: new MoneyZAR(40_000m),
            OriginalUifAmount:   new MoneyZAR(150m),
            AdjustedUifAmount:   new MoneyZAR(145m),
            AdjustmentDate: new DateOnly(2026, 2, 28), Notes: null);

        var result = Emp701Generator.Generate("tenant-zenowethu", "2025",
            new[] { record1, record2, record3 }, FixedGeneratedAt);

        result.IsSuccess.Should().BeTrue();
        var lines = SplitLines(result.Value);
        var tLine = lines.First(l => l.StartsWith("T;"));
        var fields = tLine.Split(';');

        // T;{count};{totalPayeDiff};{totalGrossDiff};{totalUifDiff}
        fields[1].Should().Be("3",       "record count must be 3");
        fields[2].Should().Be("1300.00", "total PAYE diff: 1000+500-200=1300");
        fields[3].Should().Be("3500.00", "total Gross diff: 2000+1500+0=3500");
        fields[4].Should().Be("5.00",    "total UIF diff: 0+10-5=5");
    }

    // ── Generate_NegativeDifference_FormattedCorrectly ────────────────────────

    [Fact]
    public void Generate_NegativeDifference_FormattedCorrectly()
    {
        // TC-COMP-EMP701-003: When AdjustedPaye < OriginalPaye the diff must appear as a negative
        // value in the CSV (e.g. "-500.00").
        var record = CreateRecord(
            originalPaye:  new MoneyZAR(10_000m),
            adjustedPaye:  new MoneyZAR(9_500m),   // -500 PAYE
            originalGross: new MoneyZAR(50_000m),
            adjustedGross: new MoneyZAR(50_000m),
            originalUif:   new MoneyZAR(177.12m),
            adjustedUif:   new MoneyZAR(177.12m));

        var result = Emp701Generator.Generate("tenant-zenowethu", "2025",
            new[] { record }, FixedGeneratedAt);

        result.IsSuccess.Should().BeTrue();
        var lines = SplitLines(result.Value);

        var dLine = lines.First(l => l.StartsWith("D;"));
        dLine.Should().Contain("-500.00", "negative PAYE difference must be prefixed with minus");

        var tLine = lines.First(l => l.StartsWith("T;"));
        tLine.Should().Contain("-500.00", "T record total must also reflect the negative difference");
    }

    // ── Generate_NullNotes_OutputsEmptyField ──────────────────────────────────

    [Fact]
    public void Generate_NullNotes_OutputsEmptyField()
    {
        // TC-COMP-EMP701-004: When Notes is null the last D-line field must be an empty string.
        var record = DefaultRecord(notes: null);

        var result = Emp701Generator.Generate("tenant-zenowethu", "2025",
            new[] { record }, FixedGeneratedAt);

        result.IsSuccess.Should().BeTrue();
        var dLine = SplitLines(result.Value)
                                .First(l => l.StartsWith("D;"));

        dLine.Should().EndWith(";", "null Notes must produce a trailing semicolon with empty last field");
    }

    // ── Generate_WithNotes_OutputsNotes ──────────────────────────────────────

    [Fact]
    public void Generate_WithNotes_OutputsNotes()
    {
        // TC-COMP-EMP701-005: When Notes is set it must appear as the last field in the D line.
        const string notes = "Bonus payment included in March 2025 payroll run";
        var record = DefaultRecord(notes: notes);

        var result = Emp701Generator.Generate("tenant-zenowethu", "2025",
            new[] { record }, FixedGeneratedAt);

        result.IsSuccess.Should().BeTrue();
        var dLine = SplitLines(result.Value)
                                .First(l => l.StartsWith("D;"));

        dLine.Should().Contain(notes, "Notes must appear verbatim in the D record");
        dLine.Should().EndWith(notes.TrimEnd(), "Notes is the last field in the D record");
    }

    // ── Generate_EmptyRecords_ReturnsFailure ──────────────────────────────────

    [Fact]
    public void Generate_EmptyRecords_ReturnsFailure()
    {
        // TC-COMP-EMP701-006: An empty records list must return a failure result.
        var result = Emp701Generator.Generate("tenant-zenowethu", "2025",
            Array.Empty<Emp701AdjustmentRecord>(), FixedGeneratedAt);

        result.IsFailure.Should().BeTrue("at least one record is required");
        result.Error.Code.Should().Be(ZenoHR.Domain.Errors.ZenoHrErrorCode.ValidationFailed);
    }

    // ── Generate_EmptyTenantId_ReturnsFailure ─────────────────────────────────

    [Fact]
    public void Generate_EmptyTenantId_ReturnsFailure()
    {
        // TC-COMP-EMP701-007: An empty or whitespace tenantId must return a failure result.
        var result = Emp701Generator.Generate(string.Empty, "2025",
            new[] { DefaultRecord() }, FixedGeneratedAt);

        result.IsFailure.Should().BeTrue("tenantId is required");
        result.Error.Code.Should().Be(ZenoHR.Domain.Errors.ZenoHrErrorCode.RequiredFieldMissing);
    }

    // ── Generate_InvalidTaxYear_ReturnsFailure ────────────────────────────────

    [Fact]
    public void Generate_InvalidTaxYear_ReturnsFailure()
    {
        // TC-COMP-EMP701-008: A non-numeric taxYear must return a failure result.
        var result = Emp701Generator.Generate("tenant-zenowethu", "ABCD",
            new[] { DefaultRecord() }, FixedGeneratedAt);

        result.IsFailure.Should().BeTrue("taxYear must be a 4-digit numeric year");
        result.Error.Code.Should().Be(ZenoHR.Domain.Errors.ZenoHrErrorCode.InvalidFormat);
    }

    // ── Generate_MonetaryAmounts_FormattedWithInvariantCulture ───────────────

    [Fact]
    public void Generate_MonetaryAmounts_FormattedWithInvariantCulture()
    {
        // TC-COMP-EMP701-009: Monetary amounts must use '.' as decimal separator (InvariantCulture).
        // R2500.75 must appear as "2500.75" not "2500,75".
        var record = CreateRecord(
            originalPaye:  new MoneyZAR(2_000m),
            adjustedPaye:  new MoneyZAR(4_500.75m),
            originalGross: new MoneyZAR(20_000m),
            adjustedGross: new MoneyZAR(22_500.75m),
            originalUif:   new MoneyZAR(100m),
            adjustedUif:   new MoneyZAR(100m));

        var result = Emp701Generator.Generate("tenant-zenowethu", "2025",
            new[] { record }, FixedGeneratedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("4500.75",
            "adjusted PAYE of 4500.75 must use period decimal separator");
        result.Value.Should().Contain("2500.75",
            "PAYE difference of 2500.75 must use period decimal separator");
        result.Value.Should().NotContain("2500,75",
            "comma decimal separator must never appear (locale-safe)");
    }

    // ── Generate_AdjustmentDate_FormattedCorrectly ────────────────────────────

    [Fact]
    public void Generate_AdjustmentDate_FormattedCorrectly()
    {
        // TC-COMP-EMP701-010: DateOnly(2026, 2, 28) must appear as "2026-02-28" in the D record.
        var record = CreateRecord(
            originalPaye:  new MoneyZAR(10_000m), adjustedPaye: new MoneyZAR(11_000m),
            originalGross: new MoneyZAR(50_000m), adjustedGross: new MoneyZAR(52_000m),
            originalUif:   new MoneyZAR(177.12m), adjustedUif:  new MoneyZAR(177.12m),
            adjustmentDate: new DateOnly(2026, 2, 28));

        var result = Emp701Generator.Generate("tenant-zenowethu", "2025",
            new[] { record }, FixedGeneratedAt);

        result.IsSuccess.Should().BeTrue();
        var dLine = SplitLines(result.Value)
                                .First(l => l.StartsWith("D;"));

        dLine.Should().Contain("2026-02-28",
            "adjustment date must be formatted as yyyy-MM-dd");
    }

    // ── Generate_DifferenceProperties_ComputedCorrectly ──────────────────────

    [Fact]
    public void Generate_DifferenceProperties_ComputedCorrectly()
    {
        // TC-COMP-EMP701-011: PayeDifference = AdjustedPayeAmount - OriginalPayeAmount
        var record = CreateRecord(
            originalPaye:  new MoneyZAR(10_000m),
            adjustedPaye:  new MoneyZAR(12_500m),
            originalGross: new MoneyZAR(50_000m),
            adjustedGross: new MoneyZAR(55_000m),
            originalUif:   new MoneyZAR(150m),
            adjustedUif:   new MoneyZAR(177.12m));

        record.PayeDifference.Amount.Should().Be(2_500m,   "PAYE diff = 12500 - 10000 = 2500");
        record.GrossDifference.Amount.Should().Be(5_000m,  "Gross diff = 55000 - 50000 = 5000");
        record.UifDifference.Amount.Should().Be(27.12m,    "UIF diff = 177.12 - 150 = 27.12");
    }
}
