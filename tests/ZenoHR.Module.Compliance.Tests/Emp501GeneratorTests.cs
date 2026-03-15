// TC-COMP-EMP501: EMP501 annual reconciliation generator tests.
// REQ-COMP-002, CTL-SARS-006: Annual reconciliation of EMP201 totals against IRP5/IT3a certificates.
// Tests cover CSV structure (H/D/S/T records), validation discrepancy detection, and summary report content.

using FluentAssertions;
using ZenoHR.Infrastructure.Services.Filing.Emp501;

namespace ZenoHR.Module.Compliance.Tests;

/// <summary>
/// Tests for <see cref="Emp501Generator"/> covering:
/// - CSV record structure (H, D, S, T records)
/// - Aggregate totals in T record
/// - Validation: PAYE mismatch, Gross mismatch, duplicate certificates, missing months
/// - Summary report content
/// </summary>
public sealed class Emp501GeneratorTests
{
    private readonly Emp501Generator _generator = new();

    // ── Test fixture ──────────────────────────────────────────────────────────

    /// <summary>Creates a fully valid Emp501Data with 1 month and 1 employee where totals reconcile.</summary>
    private static Emp501Data CreateValidData() => new()
    {
        TenantId        = "tenant-zenowethu",
        EmployerTaxRef  = "1234567890",
        EmployerName    = "Zenowethu (Pty) Ltd",
        EmployerAddress = "123 Main St, Johannesburg",
        TaxYear         = "2026",
        MonthlySubmissions = new List<Emp201MonthlyEntry>
        {
            new() { Period = "2025-03", TotalGross = 100_000m, TotalPaye = 20_000m,
                    TotalUifEmployee = 1_000m, TotalUifEmployer = 1_000m, TotalSdl = 1_000m,
                    Filed = true, FiledDate = new DateOnly(2025, 4, 7) },
            new() { Period = "2025-04", TotalGross = 100_000m, TotalPaye = 20_000m,
                    TotalUifEmployee = 1_000m, TotalUifEmployer = 1_000m, TotalSdl = 1_000m,
                    Filed = true, FiledDate = new DateOnly(2025, 5, 7) },
            new() { Period = "2025-05", TotalGross = 100_000m, TotalPaye = 20_000m,
                    TotalUifEmployee = 1_000m, TotalUifEmployer = 1_000m, TotalSdl = 1_000m,
                    Filed = true, FiledDate = new DateOnly(2025, 6, 7) },
            new() { Period = "2025-06", TotalGross = 100_000m, TotalPaye = 20_000m,
                    TotalUifEmployee = 1_000m, TotalUifEmployer = 1_000m, TotalSdl = 1_000m,
                    Filed = true, FiledDate = new DateOnly(2025, 7, 7) },
            new() { Period = "2025-07", TotalGross = 100_000m, TotalPaye = 20_000m,
                    TotalUifEmployee = 1_000m, TotalUifEmployer = 1_000m, TotalSdl = 1_000m,
                    Filed = true, FiledDate = new DateOnly(2025, 8, 7) },
            new() { Period = "2025-08", TotalGross = 100_000m, TotalPaye = 20_000m,
                    TotalUifEmployee = 1_000m, TotalUifEmployer = 1_000m, TotalSdl = 1_000m,
                    Filed = true, FiledDate = new DateOnly(2025, 9, 7) },
            new() { Period = "2025-09", TotalGross = 100_000m, TotalPaye = 20_000m,
                    TotalUifEmployee = 1_000m, TotalUifEmployer = 1_000m, TotalSdl = 1_000m,
                    Filed = true, FiledDate = new DateOnly(2025, 10, 7) },
            new() { Period = "2025-10", TotalGross = 100_000m, TotalPaye = 20_000m,
                    TotalUifEmployee = 1_000m, TotalUifEmployer = 1_000m, TotalSdl = 1_000m,
                    Filed = true, FiledDate = new DateOnly(2025, 11, 7) },
            new() { Period = "2025-11", TotalGross = 100_000m, TotalPaye = 20_000m,
                    TotalUifEmployee = 1_000m, TotalUifEmployer = 1_000m, TotalSdl = 1_000m,
                    Filed = true, FiledDate = new DateOnly(2025, 12, 7) },
            new() { Period = "2025-12", TotalGross = 100_000m, TotalPaye = 20_000m,
                    TotalUifEmployee = 1_000m, TotalUifEmployer = 1_000m, TotalSdl = 1_000m,
                    Filed = true, FiledDate = new DateOnly(2026, 1, 7) },
            new() { Period = "2026-01", TotalGross = 100_000m, TotalPaye = 20_000m,
                    TotalUifEmployee = 1_000m, TotalUifEmployer = 1_000m, TotalSdl = 1_000m,
                    Filed = true, FiledDate = new DateOnly(2026, 2, 7) },
            new() { Period = "2026-02", TotalGross = 200_000m, TotalPaye = 40_000m,
                    TotalUifEmployee = 2_000m, TotalUifEmployer = 2_000m, TotalSdl = 2_000m,
                    Filed = true, FiledDate = new DateOnly(2026, 3, 9) },
        },
        EmployeeEntries = new List<Emp501EmployeeEntry>
        {
            new() { EmployeeId = "emp-1", EmployeeName = "Sarah Nkosi", IdNumber = "8801015800082",
                    TaxRef = "9876543210",
                    AnnualGross = 1_300_000m, AnnualPaye = 260_000m,
                    AnnualUifEmployee = 13_000m, AnnualUifEmployer = 13_000m, AnnualSdl = 13_000m,
                    AnnualEti = 0m, AnnualMedicalCredit = 0m,
                    Irp5Code = "IRP5", CertificateNumber = "IRP5-2026-001" },
        },
    };

    /// <summary>Creates data with mismatched PAYE totals between EMP201 months and employee entries.</summary>
    private static Emp501Data CreatePayeMismatchData()
    {
        var valid = CreateValidData();
        // Employee PAYE sum = 260,000 but monthly total = 260,000 → tamper monthly to reduce Feb PAYE
        var tamperedMonths = valid.MonthlySubmissions
            .Select(m => m with { TotalPaye = m.TotalPaye - (m.Period == "2026-02" ? 32_580m : 0m) })
            .ToList();
        return valid with { MonthlySubmissions = tamperedMonths };
    }

    /// <summary>Creates data with mismatched Gross totals.</summary>
    private static Emp501Data CreateGrossMismatchData()
    {
        var valid = CreateValidData();
        var tamperedMonths = valid.MonthlySubmissions
            .Select(m => m with { TotalGross = m.TotalGross - (m.Period == "2026-02" ? 50_000m : 0m) })
            .ToList();
        return valid with { MonthlySubmissions = tamperedMonths };
    }

    /// <summary>Creates data with two employees sharing the same certificate number.</summary>
    private static Emp501Data CreateDuplicateCertData()
    {
        var valid = CreateValidData();
        var entries = new List<Emp501EmployeeEntry>(valid.EmployeeEntries)
        {
            new() { EmployeeId = "emp-2", EmployeeName = "Thabo Molefe", IdNumber = "9001015800083",
                    TaxRef = "1111111111",
                    AnnualGross = 0m, AnnualPaye = 0m,
                    AnnualUifEmployee = 0m, AnnualUifEmployer = 0m, AnnualSdl = 0m,
                    AnnualEti = 0m, AnnualMedicalCredit = 0m,
                    Irp5Code = "IT3a",
                    // Intentionally duplicate certificate number
                    CertificateNumber = "IRP5-2026-001" },
        };
        // Adjust monthly totals to still match (both employees have 0 additions beyond first)
        return valid with { EmployeeEntries = entries };
    }

    // ── GenerateReconciliationCsv ─────────────────────────────────────────────

    [Fact]
    public void GenerateReconciliationCsv_ReturnsHRecord_WithCorrectFormat()
    {
        // TC-COMP-EMP501-001: H record must contain EMP501, TaxYear, EmployerTaxRef, EmployerName
        var csv = _generator.GenerateReconciliationCsv(CreateValidData());
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var hLine = lines.FirstOrDefault(l => l.StartsWith("H;"));
        hLine.Should().NotBeNull("CSV must contain an H record");
        hLine.Should().Contain("EMP501");
        hLine.Should().Contain("2026");
        hLine.Should().Contain("1234567890");
        hLine.Should().Contain("Zenowethu (Pty) Ltd");
    }

    [Fact]
    public void GenerateReconciliationCsv_ReturnsDRecord_PerEmployee()
    {
        // TC-COMP-EMP501-002: One D record per employee entry must be present in the CSV
        var data = CreateValidData();
        var csv  = _generator.GenerateReconciliationCsv(data);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var dLines = lines.Where(l => l.StartsWith("D;")).ToList();
        dLines.Should().HaveCount(data.EmployeeEntries.Count,
            "one D record per employee entry is required");

        // Verify D record contains certificate number and IRP5 code
        dLines.First().Should().Contain("IRP5-2026-001");
        dLines.First().Should().Contain("IRP5");
    }

    [Fact]
    public void GenerateReconciliationCsv_ReturnsTRecord_WithAggregatedTotals()
    {
        // TC-COMP-EMP501-003: T record must contain aggregate totals derived from employee entries
        var data = CreateValidData();
        var csv  = _generator.GenerateReconciliationCsv(data);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var tLine = lines.FirstOrDefault(l => l.StartsWith("T;"));
        tLine.Should().NotBeNull("CSV must contain a T (totals) record");

        // T record contains: gross=1300000.00, paye=260000.00, employee count=1
        tLine.Should().Contain("1300000.00");
        tLine.Should().Contain("260000.00");
        tLine.Should().Contain(";1");   // employee count
    }

    [Fact]
    public void GenerateReconciliationCsv_ReturnsSRecords_PerMonth()
    {
        // TC-COMP-EMP501-004: One S record per monthly submission must be in the CSV
        var data  = CreateValidData();
        var csv   = _generator.GenerateReconciliationCsv(data);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var sLines = lines.Where(l => l.StartsWith("S;")).ToList();
        sLines.Should().HaveCount(data.MonthlySubmissions.Count,
            "one S record per monthly EMP201 submission is required");
    }

    // ── ValidateReconciliation ────────────────────────────────────────────────

    [Fact]
    public void ValidateReconciliation_ReturnEmpty_WhenPayeTotalsMatch()
    {
        // TC-COMP-EMP501-005: No PAYE discrepancy when monthly sums match employee annual totals
        var issues = _generator.ValidateReconciliation(CreateValidData());

        issues.Should().NotContain(i => i.Contains("PAYE"),
            "no PAYE discrepancy should be reported when totals match");
    }

    [Fact]
    public void ValidateReconciliation_ReturnsDiscrepancy_WhenPayeMismatch()
    {
        // TC-COMP-EMP501-006: PAYE mismatch between monthly EMP201 total and employee certificates
        var issues = _generator.ValidateReconciliation(CreatePayeMismatchData());

        issues.Should().Contain(i => i.Contains("PAYE"),
            "a PAYE discrepancy message must be returned when totals do not match");
    }

    [Fact]
    public void ValidateReconciliation_ReturnsDiscrepancy_WhenGrossMismatch()
    {
        // TC-COMP-EMP501-007: Gross mismatch between monthly EMP201 total and employee certificates
        var issues = _generator.ValidateReconciliation(CreateGrossMismatchData());

        issues.Should().Contain(i => i.Contains("Gross"),
            "a Gross discrepancy message must be returned when totals do not match");
    }

    [Fact]
    public void ValidateReconciliation_ReturnsDiscrepancy_WhenDuplicateCertificate()
    {
        // TC-COMP-EMP501-008: Duplicate certificate number across employees must be flagged
        var issues = _generator.ValidateReconciliation(CreateDuplicateCertData());

        issues.Should().Contain(i => i.Contains("IRP5-2026-001"),
            "duplicate certificate number must be reported");
    }

    [Fact]
    public void ValidateReconciliation_ReturnsDiscrepancy_WhenMonthsMissing()
    {
        // TC-COMP-EMP501-009: Missing monthly period entries for the SA tax year must be warned
        var valid  = CreateValidData();
        // Remove March 2025 and April 2025 from submissions
        var sparse = valid with
        {
            MonthlySubmissions = valid.MonthlySubmissions
                .Where(m => m.Period != "2025-03" && m.Period != "2025-04")
                .ToList(),
            // Also adjust employee gross/paye to avoid gross mismatch masking the missing-month warning
            EmployeeEntries = new List<Emp501EmployeeEntry>
            {
                valid.EmployeeEntries[0] with
                {
                    AnnualGross = valid.MonthlySubmissions
                        .Where(m => m.Period != "2025-03" && m.Period != "2025-04")
                        .Sum(m => m.TotalGross),
                    AnnualPaye  = valid.MonthlySubmissions
                        .Where(m => m.Period != "2025-03" && m.Period != "2025-04")
                        .Sum(m => m.TotalPaye),
                }
            },
        };

        var issues = _generator.ValidateReconciliation(sparse);

        issues.Should().Contain(i => i.Contains("2025-03") || i.Contains("Missing"),
            "missing monthly periods must be reported");
    }

    // ── GenerateSummaryReport ─────────────────────────────────────────────────

    [Fact]
    public void GenerateSummaryReport_ContainsTaxYearAndEmployerName()
    {
        // TC-COMP-EMP501-010: Summary report must include the tax year and employer name
        var report = _generator.GenerateSummaryReport(CreateValidData());

        report.Should().Contain("2026");
        report.Should().Contain("Zenowethu (Pty) Ltd");
    }

    [Fact]
    public void GenerateSummaryReport_ContainsPassStatus_WhenDataIsValid()
    {
        // TC-COMP-EMP501-011: PASS status must appear when reconciliation has no discrepancies
        var report = _generator.GenerateSummaryReport(CreateValidData());

        report.Should().Contain("PASS");
    }
}
